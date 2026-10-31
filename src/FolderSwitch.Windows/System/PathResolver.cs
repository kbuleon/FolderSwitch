using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// Resolves items to the path to open (<see cref="Resolve"/>) and checks that a path is reachable,
/// within a timeout (<see cref="ExistsAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution.</b> No disk or network access, never throws. The known folders come from
/// <c>SHGetKnownFolderPath</c> with <c>KF_FLAG_DONT_VERIFY</c>: without it the shell checks that the
/// folder is there, which for a folder redirected to a share means a network call. Program Files is
/// the 64-bit one. Temp is <see cref="Path.GetTempPath"/> without its trailing separator. OneDrive is
/// the <c>OneDrive</c> environment variable that the OneDrive client sets (<c>null</c> without it).
/// This PC and the Recycle Bin are <c>shell:</c> forms. A path item has its environment variables
/// expanded; a <c>shell:</c> or <c>::{CLSID}</c> form is returned as is, and nothing is normalized
/// (UNC paths stay UNC paths).
/// </para>
/// <para>
/// <b>Existence.</b> A <c>shell:</c> or <c>::{CLSID}</c> form exists without any check. Any other path
/// is checked (<see cref="Directory.Exists"/>, then <see cref="File.Exists"/>) on a thread of its own,
/// not a thread-pool one: on an unreachable server the check holds its thread for about 20 s (21 s
/// measured for <c>\\10.255.255.1\x</c>), longer if the network stack retries. The returned task
/// completes with <c>false</c> when the timeout expires; the check then goes on and its result is
/// ignored. Such abandoned checks are bounded: while <see cref="DefaultMaxAbandoned"/> of them are
/// still running, every other check returns <c>false</c> at once rather than start another thread (a
/// soft bound: checks that start together may each pass it). And while a check abandoned on a network
/// share is still running, other checks on that share return <c>false</c> at once: they would block
/// the same way, and would use up the bound that local paths need. A share is a UNC share
/// (<c>\\server\share</c>) or a mapped network drive (a drive letter whose <c>GetDriveType</c> is
/// <c>DRIVE_REMOTE</c>, asked on the check's own thread, so that the question cannot hold the caller
/// either; a drive whose answer has not come when its check is abandoned counts as a share too). Not
/// a local drive: a local path may cross into the network (a symbolic link, a mounted folder), and one
/// slow link must not fail every check of its drive. Never throws.
/// </para>
/// </remarks>
public sealed class PathResolver : IPathResolver
{
    /// <summary>How many abandoned checks may run at once.</summary>
    public const int DefaultMaxAbandoned = 4;

    private const string MyComputerFolder = "shell:MyComputerFolder";
    private const string RecycleBinFolder = "shell:RecycleBinFolder";
    private const string ProbeThreadName = "FolderSwitch existence check";

    /// <summary><c>DRIVE_REMOTE</c>, what <c>GetDriveType</c> says of a mapped network drive.</summary>
    private const uint DriveTypeRemote = 4;

    private static readonly Task<bool> True = Task.FromResult(true);
    private static readonly Task<bool> False = Task.FromResult(false);

    private readonly Func<string, bool> _probe;
    private readonly Func<string, bool> _isRemoteDrive;
    private readonly int _maxAbandoned;

    // ---- Guarded by _gate ----
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _hungShares = new(StringComparer.OrdinalIgnoreCase);
    private int _abandoned;

    public PathResolver()
        : this(Probe, DefaultMaxAbandoned, IsRemoteDrive)
    {
    }

    /// <summary>
    /// Test seam: another existence check, run on the check thread in place of
    /// <see cref="Directory.Exists"/> / <see cref="File.Exists"/>, another bound on abandoned checks, and
    /// another drive-type question (given a drive root such as <c>H:\</c>, whether it is a network drive;
    /// <c>null</c>: no drive is).
    /// </summary>
    internal PathResolver(Func<string, bool> probe, int maxAbandoned, Func<string, bool>? isRemoteDrive = null)
    {
        _probe = probe;
        _maxAbandoned = maxAbandoned;
        _isRemoteDrive = isRemoteDrive ?? (static _ => false);
    }

    /// <summary>Test seam: how many abandoned checks are still running.</summary>
    internal int AbandonedCount
    {
        get
        {
            lock (_gate)
            {
                return _abandoned;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A special folder wins over a path when an item has both. <c>null</c> for an item with neither, a
    /// blank path, an unknown special folder, OneDrive when it is not installed, or a known folder the
    /// shell does not know.
    /// </remarks>
    public string? Resolve(Item item)
    {
        try
        {
            if (item?.Special is { } special)
            {
                return ResolveSpecial(special);
            }

            return ResolvePath(item?.Path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>false</c> for a blank path, a timeout that is zero or negative, or when the check cannot run
    /// (too many abandoned checks, the share already hung). <see cref="Timeout.InfiniteTimeSpan"/>
    /// waits for the check however long it takes. Continuations are queued, never run inline on the
    /// check thread or the timer thread.
    /// </remarks>
    public Task<bool> ExistsAsync(string path, TimeSpan timeout)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return False;
            }

            if (IsShellForm(path))
            {
                return True;
            }

            // Beyond what a timer takes (about 24 days), as good as infinite.
            var infinite = timeout == Timeout.InfiniteTimeSpan || timeout.TotalMilliseconds >= int.MaxValue;
            if (!infinite && timeout <= TimeSpan.Zero)
            {
                return False;
            }

            var share = UncShareOf(path);
            var drive = share is null ? DriveOf(path) : null;
            lock (_gate)
            {
                if (_abandoned >= _maxAbandoned || ((share ?? drive) is { } key && _hungShares.ContainsKey(key)))
                {
                    return False;
                }
            }

            var check = new Check(this, path, share, drive);
            check.Start(infinite ? null : timeout);
            return check.Result;
        }
        catch (Exception)
        {
            return False;
        }
    }

    private static string? ResolveSpecial(SpecialFolderKey key) => key switch
    {
        SpecialFolderKey.Desktop => KnownFolder(PInvoke.FOLDERID_Desktop),
        SpecialFolderKey.Documents => KnownFolder(PInvoke.FOLDERID_Documents),
        SpecialFolderKey.Downloads => KnownFolder(PInvoke.FOLDERID_Downloads),
        SpecialFolderKey.Pictures => KnownFolder(PInvoke.FOLDERID_Pictures),
        SpecialFolderKey.Music => KnownFolder(PInvoke.FOLDERID_Music),
        SpecialFolderKey.Videos => KnownFolder(PInvoke.FOLDERID_Videos),
        SpecialFolderKey.OneDrive => OneDrive(),
        SpecialFolderKey.UserProfile => KnownFolder(PInvoke.FOLDERID_Profile),
        SpecialFolderKey.ThisPC => MyComputerFolder,
        SpecialFolderKey.RecycleBin => RecycleBinFolder,

        // FOLDERID_ProgramFilesX64 fails in a 32-bit process; FOLDERID_ProgramFiles is then the x86 one.
        SpecialFolderKey.ProgramFiles => KnownFolder(PInvoke.FOLDERID_ProgramFilesX64) ?? KnownFolder(PInvoke.FOLDERID_ProgramFiles),
        SpecialFolderKey.RoamingAppData => KnownFolder(PInvoke.FOLDERID_RoamingAppData),
        SpecialFolderKey.LocalAppData => KnownFolder(PInvoke.FOLDERID_LocalAppData),
        SpecialFolderKey.Temp => Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
        SpecialFolderKey.Startup => KnownFolder(PInvoke.FOLDERID_Startup),
        SpecialFolderKey.Windows => KnownFolder(PInvoke.FOLDERID_Windows),
        _ => null,
    };

    private static string? ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return IsShellForm(path) ? path : Environment.ExpandEnvironmentVariables(path);
    }

    /// <summary>The path of a known folder, without checking that it exists; <c>null</c> if the shell fails.</summary>
    private static unsafe string? KnownFolder(Guid id)
    {
        PWSTR path = default;
        try
        {
            var result = PInvoke.SHGetKnownFolderPath(&id, KNOWN_FOLDER_FLAG.KF_FLAG_DONT_VERIFY, default, &path);
            return result.Succeeded && path.Value is not null && path.Value[0] != '\0' ? path.ToString() : null;
        }
        finally
        {
            // To be freed whether the call succeeded or not.
            PInvoke.CoTaskMemFree(path.Value);
        }
    }

    private static string? OneDrive()
    {
        var path = Environment.GetEnvironmentVariable("OneDrive");
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static bool IsShellForm(string path) =>
        path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("::{", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>\\server\share</c> for a path on a UNC share, <c>null</c> for any other path (a device path to
    /// a drive, <c>\\?\C:\...</c>, included).
    /// </summary>
    private static string? UncShareOf(string path)
    {
        var normalized = path.Replace('/', '\\');
        if (!normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return null;
        }

        if ((normalized.StartsWith(@"\\?\", StringComparison.Ordinal) || normalized.StartsWith(@"\\.\", StringComparison.Ordinal))
            && !normalized.AsSpan(4).StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var root = Path.GetPathRoot(normalized);
        return string.IsNullOrEmpty(root) ? null : Path.TrimEndingDirectorySeparator(root);
    }

    /// <summary><c>H:</c> for a path on drive H (<c>H:\...</c>, <c>h:</c>), <c>null</c> for any other path.</summary>
    private static string? DriveOf(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'
            ? char.ToUpperInvariant(path[0]) + ":"
            : null;

    private static bool Probe(string path) => Directory.Exists(path) || File.Exists(path);

    /// <summary>Whether the drive of <paramref name="root"/> (<c>H:\</c>) is a network drive. On the check's thread.</summary>
    private static bool IsRemoteDrive(string root) => PInvoke.GetDriveType(root) == DriveTypeRemote;

    /// <summary>Under the gate: a check overran and goes on in the background.</summary>
    private void AddAbandoned(string? share)
    {
        _abandoned++;
        if (share is not null)
        {
            _hungShares[share] = _hungShares.GetValueOrDefault(share) + 1;
        }
    }

    /// <summary>Under the gate: an abandoned check has ended.</summary>
    private void RemoveAbandoned(string? share)
    {
        _abandoned--;
        if (share is not null && _hungShares.TryGetValue(share, out var count))
        {
            if (count <= 1)
            {
                _hungShares.Remove(share);
            }
            else
            {
                _hungShares[share] = count - 1;
            }
        }
    }

    /// <summary>
    /// One existence check: its thread, its timer and the task given to the caller. <paramref name="share"/> is the UNC
    /// share of the path; <paramref name="drive"/> its drive letter otherwise, a share only if the drive is remote.
    /// </summary>
    private sealed class Check(PathResolver owner, string path, string? share, string? drive)
    {
        private const int DriveNotAsked = 0;
        private const int DriveAsking = 1;
        private const int DriveLocal = 2;
        private const int DriveRemote = 3;

        private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CheckState _state; // guarded by owner._gate
        private string? _hungKey; // guarded by owner._gate: the share counted as hung while this check is abandoned
        private int _driveKind; // DriveNotAsked until the check thread asks
        private Timer? _timer; // referenced here so that it is not collected before it fires

        private enum CheckState
        {
            Running,
            Completed,
            Abandoned,
        }

        public Task<bool> Result => _result.Task;

        /// <param name="timeout"><c>null</c>: no timeout.</param>
        public void Start(TimeSpan? timeout)
        {
            var thread = new Thread(Run) { IsBackground = true, Name = ProbeThreadName };

            // Set before the thread starts, so the thread sees it. It may fire first: Run then ends an
            // abandoned check, which is accounted for.
            if (timeout is { } delay)
            {
                _timer = new Timer(static state => ((Check)state!).OnTimeout(), this, delay, Timeout.InfiniteTimeSpan);
            }

            try
            {
                thread.UnsafeStart();
            }
            catch
            {
                Finish(exists: false);
                throw;
            }
        }

        private void Run()
        {
            bool exists;
            try
            {
                if (drive is not null)
                {
                    // Before the probe: a network drive is told apart here, where waiting costs the caller nothing.
                    Volatile.Write(ref _driveKind, DriveAsking);
                    Volatile.Write(ref _driveKind, owner._isRemoteDrive(drive + @"\") ? DriveRemote : DriveLocal);
                }

                exists = owner._probe(path);
            }
            catch (Exception)
            {
                exists = false;
            }

            Finish(exists);
        }

        private void Finish(bool exists)
        {
            _timer?.Dispose();
            bool abandoned;
            lock (owner._gate)
            {
                abandoned = _state == CheckState.Abandoned;
                if (abandoned)
                {
                    owner.RemoveAbandoned(_hungKey);
                }

                _state = CheckState.Completed;
            }

            if (!abandoned)
            {
                _result.TrySetResult(exists);
            }
        }

        private void OnTimeout()
        {
            lock (owner._gate)
            {
                if (_state != CheckState.Running)
                {
                    return;
                }

                // A drive whose type question is still unanswered is one where that question itself hangs: no local
                // drive does. One not asked yet (a check thread that has not run) proves nothing.
                _state = CheckState.Abandoned;
                _hungKey = share ?? (Volatile.Read(ref _driveKind) is DriveAsking or DriveRemote ? drive : null);
                owner.AddAbandoned(_hungKey);
            }

            _result.TrySetResult(false);
        }
    }
}
