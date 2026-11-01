using System.Diagnostics;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Activation;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// A browser instance started by a test, and nothing else: a fresh temporary profile (so that it neither joins nor
/// disturbs the user's own browser), a window found through the process tree of the process the test started, and,
/// on dispose, only that tree killed. A browser window the test did not start is never looked at, focused or closed.
/// </summary>
internal sealed class BrowserSession : IDisposable
{
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] FirefoxPreferences =
    [
        "user_pref(\"browser.shell.checkDefaultBrowser\", false);",
        "user_pref(\"browser.aboutwelcome.enabled\", false);",
        "user_pref(\"trailhead.firstrun.didSeeAboutWelcome\", true);",
        "user_pref(\"datareporting.policy.dataSubmissionEnabled\", false);",
        "user_pref(\"datareporting.policy.dataSubmissionPolicyBypassNotification\", true);",
        "user_pref(\"toolkit.telemetry.reportingpolicy.firstRun\", false);",
        "user_pref(\"browser.startup.homepage_override.mstone\", \"ignore\");",
        "user_pref(\"startup.homepage_welcome_url\", \"\");",
        "user_pref(\"startup.homepage_welcome_url.additional\", \"\");",
        "user_pref(\"browser.startup.upgradeDialog.enabled\", false);",
        "user_pref(\"browser.sessionstore.resume_from_crash\", false);",
        "user_pref(\"browser.tabs.warnOnClose\", false);",
        "user_pref(\"app.update.auto\", false);",
        "user_pref(\"app.update.disabledForTesting\", true);",
    ];

    private readonly TempFolder _profile;
    private readonly string _executable;
    private readonly int _rootPid;
    private readonly HashSet<int> _seen = [];

    private BrowserSession(TempFolder profile, string executable, int rootPid)
    {
        _profile = profile;
        _executable = executable;
        _rootPid = rootPid;
        _seen.Add(rootPid);
    }

    /// <summary>The main window of the instance (a visible, titled top-level window of the expected class).</summary>
    public nint Window { get; private set; }

    /// <summary>The path of the browser's executable, <c>null</c> when it is not installed.</summary>
    public static string? FindExecutable(BrowserKind browser)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] candidates = browser switch
        {
            BrowserKind.Firefox =>
            [
                Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
                Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe"),
            ],
            BrowserKind.Chrome =>
            [
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"),
            ],
            BrowserKind.Edge =>
            [
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
            ],
            _ => [],
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Starts the browser with a new profile on <paramref name="urls"/>, one tab each (<c>about:blank</c> when none
    /// is given), and waits for its window. The caller owns the result and disposes it, which kills the instance; if
    /// no window comes, the instance is killed and the test fails.
    /// </summary>
    public static BrowserSession Start(BrowserKind browser, params string[] urls)
    {
        var executable = FindExecutable(browser) ?? throw new InvalidOperationException($"{browser} is not installed.");
        var profile = new TempFolder();
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (browser == BrowserKind.Firefox)
        {
            // No welcome, privacy notice or update tab next to the pages asked for, no default-browser prompt.
            File.WriteAllLines(Path.Combine(profile.Path, "user.js"), FirefoxPreferences);
            info.ArgumentList.Add("-profile");
            info.ArgumentList.Add(profile.Path);
            info.ArgumentList.Add("-no-remote");
            info.ArgumentList.Add("-new-instance");
        }
        else
        {
            info.ArgumentList.Add("--user-data-dir=" + profile.Path);
            info.ArgumentList.Add("--no-first-run");
            info.ArgumentList.Add("--no-default-browser-check");
            info.ArgumentList.Add("--disable-search-engine-choice-screen");
        }

        foreach (var url in urls.Length == 0 ? ["about:blank"] : urls)
        {
            info.ArgumentList.Add(url);
        }

        BrowserSession? session = null;
        try
        {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("The browser did not start.");
            session = new BrowserSession(profile, executable, process.Id);
            session.Window = session.WaitForWindow(browser);
            return session;
        }
        catch
        {
            if (session is not null)
            {
                session.Dispose();
            }
            else
            {
                profile.Dispose();
            }

            throw;
        }
    }

    /// <summary>Kills the processes of the instance (those of the tree whose executable is the browser's), then deletes the profile.</summary>
    public void Dispose()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        do
        {
            var pids = Tree();
            pids.UnionWith(_seen);
            foreach (var pid in pids)
            {
                Kill(pid, Path.GetFileNameWithoutExtension(_executable));
            }

            if (!pids.Any(pid => IsRunning(pid, Path.GetFileNameWithoutExtension(_executable))))
            {
                break;
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        _profile.Dispose();
    }

    private nint WaitForWindow(BrowserKind browser)
    {
        var className = browser == BrowserKind.Firefox ? "MozillaWindowClass" : "Chrome_WidgetWin_1";
        var deadline = DateTime.UtcNow + WindowTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var pids = Tree();
            _seen.UnionWith(pids);
            var window = TopLevelWindows()
                .FirstOrDefault(w => IsWindowVisible(w) && pids.Contains(ProcessOf(w)) && ClassOf(w) == className && TitleLength(w) > 0);
            if (window != 0)
            {
                return window;
            }

            Thread.Sleep(100);
        }

        Assert.Fail($"No window of {browser} (process tree of {_rootPid}) within {WindowTimeout.TotalSeconds} s.");
        return 0;
    }

    /// <summary>The root process and every process descended from it that runs the browser's executable (the root may be gone).</summary>
    private HashSet<int> Tree()
    {
        var entries = Processes();
        var tree = new HashSet<int> { _rootPid };
        bool added;
        do
        {
            added = false;
            foreach (var (pid, parent, _) in entries)
            {
                if (tree.Contains(parent) && tree.Add(pid))
                {
                    added = true;
                }
            }
        }
        while (added);

        var name = Path.GetFileName(_executable);
        tree.RemoveWhere(pid => pid != _rootPid
            && !entries.Any(e => e.Pid == pid && string.Equals(e.Exe, name, StringComparison.OrdinalIgnoreCase)));
        return tree;
    }

    /// <summary>Kills the process if it still runs the browser's executable (the number may have been reused since).</summary>
    private static void Kill(int pid, string processName)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            {
                process.Kill();
            }
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Exiting.
        }
    }

    private static bool IsRunning(int pid, string processName)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited && string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static List<nint> TopLevelWindows()
    {
        var windows = new List<nint>();
        EnumWindows(
            (hwnd, _) =>
            {
                windows.Add(hwnd);
                return true;
            },
            0);
        return windows;
    }

    private static int ProcessOf(nint window)
    {
        _ = GetWindowThreadProcessId(window, out var pid);
        return (int)pid;
    }

    private static string ClassOf(nint window)
    {
        var buffer = new char[256];
        var length = GetClassNameW(window, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    private static int TitleLength(nint window) => GetWindowTextLengthW(window);

    private static List<(int Pid, int Parent, string Exe)> Processes()
    {
        var result = new List<(int, int, string)>();
        var snapshot = CreateToolhelp32Snapshot(0x2, 0);
        if (snapshot == -1)
        {
            return result;
        }

        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            for (var ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
            {
                result.Add(((int)entry.ProcessId, (int)entry.ParentProcessId, entry.ExeFile));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hwnd, [Out] char[] buffer, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLengthW(nint hwnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(nint snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(nint snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

/// <summary>
/// A fact that runs only where the browser is installed: skipped, with that reason, otherwise.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute(BrowserKind browser)
    {
        if (BrowserSession.FindExecutable(browser) is null)
        {
            Skip = $"{browser} is not installed on this machine.";
        }
    }
}
