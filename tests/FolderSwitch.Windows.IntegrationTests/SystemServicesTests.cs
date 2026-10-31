using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.SystemServices;
using Microsoft.Win32;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Path resolution, existence checks, start with Windows, system information and the single instance,
/// on the real system. Side effects stay in test-owned places: registry writes go to a key of their
/// own under <c>HKCU\Software\FolderSwitch.Tests</c> (deleted afterwards; the real <c>Run</c> key and
/// the user's theme are only read), files to a temporary folder, and named objects carry a unique name
/// per test. No window is opened, no keystroke is sent.
/// </summary>
[Trait("Category", "Desktop")]
public sealed class SystemServicesTests : IDisposable
{
    private const string TestRoot = @"Software\FolderSwitch.Tests";

    /// <summary>Not routed anywhere: a connection attempt waits until the network stack gives up.</summary>
    private const string UnreachableShare = @"\\10.255.255.1\x";

    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly string _testKey = TestRoot + @"\" + Guid.NewGuid().ToString("N")[..8];
    private readonly TempFolder _folder = new();

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_testKey, throwOnMissingSubKey: false);
        try
        {
            // Only once empty: another test may still use its own key under it.
            Registry.CurrentUser.DeleteSubKey(TestRoot, throwOnMissingSubKey: false);
        }
        catch (InvalidOperationException)
        {
        }

        _folder.Dispose();
    }

    // ---- PathResolver: resolution ----

    [Fact]
    public void Resolves_all_16_special_folders_or_null()
    {
        var sut = new PathResolver();
        var keys = Enum.GetValues<SpecialFolderKey>();
        Assert.Equal(16, keys.Length);

        foreach (var key in keys)
        {
            var resolved = sut.Resolve(new Item { Special = key });
            switch (key)
            {
                case SpecialFolderKey.OneDrive:
                    // The only one that may be missing (OneDrive not installed or not signed in).
                    Assert.Equal(NullIfBlank(Environment.GetEnvironmentVariable("OneDrive")), resolved);
                    break;
                case SpecialFolderKey.ThisPC:
                    Assert.Equal("shell:MyComputerFolder", resolved);
                    break;
                case SpecialFolderKey.RecycleBin:
                    Assert.Equal("shell:RecycleBinFolder", resolved);
                    break;
                default:
                    Assert.False(string.IsNullOrWhiteSpace(resolved), $"{key} did not resolve.");
                    Assert.True(Path.IsPathFullyQualified(resolved), $"{key}: {resolved}");
                    Assert.False(Path.EndsInDirectorySeparator(resolved), $"{key}: {resolved}");
                    break;
            }
        }
    }

    [Fact]
    public void Special_folders_match_the_shell_known_folders()
    {
        var sut = new PathResolver();
        string? Resolve(SpecialFolderKey key) => sut.Resolve(new Item { Special = key });
        static string Shell(Environment.SpecialFolder folder) =>
            Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);

        Assert.Equal(Shell(Environment.SpecialFolder.DesktopDirectory), Resolve(SpecialFolderKey.Desktop));
        Assert.Equal(Shell(Environment.SpecialFolder.MyDocuments), Resolve(SpecialFolderKey.Documents));
        Assert.Equal(Shell(Environment.SpecialFolder.MyPictures), Resolve(SpecialFolderKey.Pictures));
        Assert.Equal(Shell(Environment.SpecialFolder.MyMusic), Resolve(SpecialFolderKey.Music));
        Assert.Equal(Shell(Environment.SpecialFolder.MyVideos), Resolve(SpecialFolderKey.Videos));
        Assert.Equal(Shell(Environment.SpecialFolder.UserProfile), Resolve(SpecialFolderKey.UserProfile));
        Assert.Equal(Shell(Environment.SpecialFolder.ApplicationData), Resolve(SpecialFolderKey.RoamingAppData));
        Assert.Equal(Shell(Environment.SpecialFolder.LocalApplicationData), Resolve(SpecialFolderKey.LocalAppData));
        Assert.Equal(Shell(Environment.SpecialFolder.Startup), Resolve(SpecialFolderKey.Startup));
        Assert.Equal(Shell(Environment.SpecialFolder.Windows), Resolve(SpecialFolderKey.Windows));
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Resolve(SpecialFolderKey.Temp));

        // The 64-bit Program Files (the test host is a 64-bit process, as FolderSwitch is).
        Assert.True(Environment.Is64BitProcess);
        Assert.Equal(Shell(Environment.SpecialFolder.ProgramFiles), Resolve(SpecialFolderKey.ProgramFiles));
        Assert.Equal(Environment.GetEnvironmentVariable("ProgramW6432"), Resolve(SpecialFolderKey.ProgramFiles), ignoreCase: true);

        // No SpecialFolder for Downloads: it lives in the profile unless the user moved it.
        var downloads = Resolve(SpecialFolderKey.Downloads);
        Assert.NotNull(downloads);
        Assert.True(Path.IsPathFullyQualified(downloads));
        Assert.EndsWith("Downloads", downloads, StringComparison.OrdinalIgnoreCase);

        // The folders that are always there.
        foreach (var key in new[] { SpecialFolderKey.UserProfile, SpecialFolderKey.ProgramFiles, SpecialFolderKey.RoamingAppData, SpecialFolderKey.LocalAppData, SpecialFolderKey.Temp, SpecialFolderKey.Windows })
        {
            Assert.True(Directory.Exists(Resolve(key)), $"{key}: {Resolve(key)}");
        }
    }

    [Fact]
    public void Path_items_have_their_environment_variables_expanded()
    {
        var sut = new PathResolver();
        var profile = Environment.GetEnvironmentVariable("USERPROFILE")!;

        Assert.Equal(profile + @"\Clients", sut.Resolve(PathItem(@"%USERPROFILE%\Clients")));
        Assert.Equal(@"D:\Clients\Acme", sut.Resolve(PathItem(@"D:\Clients\Acme")));
        Assert.Equal(@"%FolderSwitchUndefined%\x", sut.Resolve(PathItem(@"%FolderSwitchUndefined%\x")));
    }

    [Fact]
    public void Shell_forms_and_UNC_paths_are_kept_as_is()
    {
        var sut = new PathResolver();

        foreach (var path in new[]
        {
            "shell:startup",
            "shell:MyComputerFolder",
            "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            @"\\nas\partage",
            @"\\nas\partage\Projets\",
        })
        {
            Assert.Equal(path, sut.Resolve(PathItem(path)));
        }
    }

    [Fact]
    public void Unresolvable_items_give_null()
    {
        var sut = new PathResolver();

        Assert.Null(sut.Resolve(null!));
        Assert.Null(sut.Resolve(new Item()));
        Assert.Null(sut.Resolve(PathItem("")));
        Assert.Null(sut.Resolve(PathItem("   ")));
        Assert.Null(sut.Resolve(new Item { Special = (SpecialFolderKey)99 }));
    }

    [Fact]
    public void A_special_folder_wins_over_a_path()
    {
        var sut = new PathResolver();

        Assert.Equal(
            "shell:RecycleBinFolder",
            sut.Resolve(new Item { Special = SpecialFolderKey.RecycleBin, Path = @"C:\Windows" }));
    }

    // ---- PathResolver: existence ----

    [Fact]
    public async Task ExistsAsync_detects_files_and_folders()
    {
        var sut = new PathResolver();
        var file = Path.Combine(_folder.Path, "note.txt");
        await File.WriteAllTextAsync(file, "FolderSwitch");
        var timeout = TimeSpan.FromSeconds(2);

        Assert.True(await sut.ExistsAsync(_folder.Path, timeout));
        Assert.True(await sut.ExistsAsync(_folder.Path + @"\", timeout));
        Assert.True(await sut.ExistsAsync(file, timeout));
        Assert.False(await sut.ExistsAsync(Path.Combine(_folder.Path, "missing"), timeout));
        Assert.False(await sut.ExistsAsync(Path.Combine(_folder.Path, "missing.txt"), timeout));
        Assert.False(await sut.ExistsAsync("", timeout));
        Assert.False(await sut.ExistsAsync("   ", timeout));
        Assert.False(await sut.ExistsAsync(_folder.Path, TimeSpan.Zero));
        Assert.True(await sut.ExistsAsync(_folder.Path, Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public async Task ExistsAsync_takes_shell_forms_for_granted()
    {
        // The probe would say no: it is never asked.
        var probed = 0;
        var sut = new PathResolver(_ => Interlocked.Increment(ref probed) < 0, PathResolver.DefaultMaxAbandoned);

        Assert.True(await sut.ExistsAsync("shell:RecycleBinFolder", TimeSpan.FromSeconds(1)));
        Assert.True(await sut.ExistsAsync("SHELL:MyComputerFolder", TimeSpan.FromSeconds(1)));
        Assert.True(await sut.ExistsAsync("::{645FF040-5081-101B-9F08-00AA002F954E}", TimeSpan.FromSeconds(1)));
        Assert.Equal(0, probed);
    }

    [Fact]
    public async Task ExistsAsync_respects_timeout()
    {
        var sut = new PathResolver();
        var stopwatch = Stopwatch.StartNew();

        var exists = await sut.ExistsAsync(UnreachableShare, TimeSpan.FromSeconds(2));

        stopwatch.Stop();
        Assert.False(exists);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2.5), $"Took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    [Fact]
    public async Task ExistsAsync_does_not_block_its_caller_and_ignores_a_late_result()
    {
        using var release = new ManualResetEventSlim();
        var sut = new PathResolver(_ => release.Wait(TimeSpan.FromSeconds(30)), PathResolver.DefaultMaxAbandoned);
        var stopwatch = Stopwatch.StartNew();

        var check = sut.ExistsAsync(@"C:\Hung", TimeSpan.FromMilliseconds(300));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100), $"The call itself took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.False(check.IsCompleted);
        Assert.False(await check);
        Assert.Equal(1, sut.AbandonedCount);

        // The check ends with "exists" after the timeout: ignored, and no longer counted.
        release.Set();
        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
        Assert.False(await check);
    }

    [Fact]
    public async Task ExistsAsync_runs_the_check_on_a_background_thread_of_its_own()
    {
        var threads = new ConcurrentBag<(bool Pool, bool Background, int Id)>();
        var sut = new PathResolver(
            _ =>
            {
                var thread = Thread.CurrentThread;
                threads.Add((thread.IsThreadPoolThread, thread.IsBackground, thread.ManagedThreadId));
                return true;
            },
            PathResolver.DefaultMaxAbandoned);

        Assert.True(await sut.ExistsAsync(@"C:\A", TimeSpan.FromSeconds(2)));
        Assert.True(await sut.ExistsAsync(@"C:\B", TimeSpan.FromSeconds(2)));

        Assert.Equal(2, threads.Count);
        Assert.All(threads, t => Assert.False(t.Pool));
        Assert.All(threads, t => Assert.True(t.Background));
        Assert.NotEqual(Environment.CurrentManagedThreadId, threads.First().Id);
    }

    [Fact]
    public async Task ExistsAsync_bounds_the_abandoned_checks()
    {
        using var release = new ManualResetEventSlim();
        var probed = new ConcurrentQueue<string>();
        var sut = new PathResolver(
            path =>
            {
                probed.Enqueue(path);
                return !path.Contains("Hung", StringComparison.Ordinal) || release.Wait(TimeSpan.FromSeconds(30));
            },
            maxAbandoned: 2);
        var shortTimeout = TimeSpan.FromMilliseconds(100);

        Assert.False(await sut.ExistsAsync(@"C:\Hung1", shortTimeout));
        Assert.False(await sut.ExistsAsync(@"D:\Hung2", shortTimeout));
        Assert.Equal(2, sut.AbandonedCount);

        // Bound reached: false at once, without a check, even for a folder that is there.
        var stopwatch = Stopwatch.StartNew();
        var refused = sut.ExistsAsync(@"C:\Fine", TimeSpan.FromSeconds(2));
        Assert.True(refused.IsCompleted);
        Assert.False(await refused);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100));
        Assert.DoesNotContain(@"C:\Fine", probed);

        // Once the hung checks end, checks run again.
        release.Set();
        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
        Assert.True(await sut.ExistsAsync(@"C:\Fine", TimeSpan.FromSeconds(2)));
        Assert.Contains(@"C:\Fine", probed);
    }

    [Fact]
    public async Task ExistsAsync_does_not_check_again_a_share_that_hangs()
    {
        using var release = new ManualResetEventSlim();
        var probed = new ConcurrentQueue<string>();
        var sut = new PathResolver(
            path =>
            {
                probed.Enqueue(path);
                return !path.StartsWith(@"\\nas\hung", StringComparison.OrdinalIgnoreCase) || release.Wait(TimeSpan.FromSeconds(30));
            },
            PathResolver.DefaultMaxAbandoned);
        var shortTimeout = TimeSpan.FromMilliseconds(100);

        Assert.False(await sut.ExistsAsync(@"\\nas\hung\Projets", shortTimeout));

        // Same share (whatever the case): false at once, no new check, no new abandoned check.
        var refused = sut.ExistsAsync(@"\\NAS\Hung\Archives", TimeSpan.FromSeconds(2));
        Assert.True(refused.IsCompleted);
        Assert.False(await refused);
        Assert.Equal(1, sut.AbandonedCount);

        // Another share of that server, and a local path, are still checked.
        Assert.True(await sut.ExistsAsync(@"\\nas\other\Projets", TimeSpan.FromSeconds(2)));
        Assert.True(await sut.ExistsAsync(@"C:\Projets", TimeSpan.FromSeconds(2)));
        Assert.Equal(new[] { @"\\nas\hung\Projets", @"\\nas\other\Projets", @"C:\Projets" }, probed);

        // The hung check ends: the share is checked again.
        release.Set();
        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
        Assert.True(await sut.ExistsAsync(@"\\nas\hung\Archives", TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ExistsAsync_tells_shares_from_device_paths_to_drives()
    {
        using var release = new ManualResetEventSlim();
        var probed = new ConcurrentQueue<string>();
        var sut = new PathResolver(
            path =>
            {
                probed.Enqueue(path);
                return !path.Contains("Hung", StringComparison.Ordinal) || release.Wait(TimeSpan.FromSeconds(30));
            },
            PathResolver.DefaultMaxAbandoned);
        var shortTimeout = TimeSpan.FromMilliseconds(100);
        try
        {
            // A share written with forward slashes is the same share.
            Assert.False(await sut.ExistsAsync("//nas/Hung/Projets", shortTimeout));
            Assert.False(await sut.ExistsAsync(@"\\nas\Hung\Archives", TimeSpan.FromSeconds(2)));

            // A device path to a drive is local: one hung check does not stop the others on that drive.
            Assert.False(await sut.ExistsAsync(@"\\?\C:\Hung", shortTimeout));
            Assert.True(await sut.ExistsAsync(@"\\?\C:\Fine", TimeSpan.FromSeconds(2)));

            Assert.Equal(new[] { "//nas/Hung/Projets", @"\\?\C:\Hung", @"\\?\C:\Fine" }, probed);
            Assert.Equal(2, sut.AbandonedCount);
        }
        finally
        {
            release.Set();
        }

        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
    }

    [Fact]
    public async Task ExistsAsync_does_not_check_again_a_mapped_network_drive_that_hangs()
    {
        // H: is a mapped drive of a server out of reach (a laptop off the VPN); C: is local.
        using var release = new ManualResetEventSlim();
        var probed = new ConcurrentQueue<string>();
        var asked = new ConcurrentQueue<string>();
        var sut = new PathResolver(
            path =>
            {
                probed.Enqueue(path);
                return !path.StartsWith("H:", StringComparison.OrdinalIgnoreCase) || release.Wait(TimeSpan.FromSeconds(30));
            },
            maxAbandoned: 2,
            isRemoteDrive: root =>
            {
                asked.Enqueue(root);
                return root.Equals(@"H:\", StringComparison.OrdinalIgnoreCase);
            });
        var shortTimeout = TimeSpan.FromMilliseconds(100);
        try
        {
            Assert.False(await sut.ExistsAsync(@"H:\Projets", shortTimeout));
            Assert.Equal(1, sut.AbandonedCount);

            // Retries on that drive (any case, any folder): false at once, no new check, no new abandoned check.
            for (var i = 0; i < 5; i++)
            {
                var refused = sut.ExistsAsync(@"h:\Archives", TimeSpan.FromSeconds(2));
                Assert.True(refused.IsCompleted);
                Assert.False(await refused);
            }

            Assert.Equal(1, sut.AbandonedCount);

            // Local folders are still checked: the bound of abandoned checks was not used up.
            Assert.True(await sut.ExistsAsync(@"C:\Users\Me\Documents", TimeSpan.FromSeconds(2)));
            Assert.Equal(new[] { @"H:\Projets", @"C:\Users\Me\Documents" }, probed);
            Assert.Contains(@"H:\", asked);
            Assert.Contains(@"C:\", asked);
        }
        finally
        {
            release.Set();
        }

        // The hung check ends: the drive is checked again.
        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
        Assert.True(await sut.ExistsAsync(@"H:\Archives", TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ExistsAsync_does_not_fail_a_whole_local_drive_for_one_slow_path()
    {
        // A local path may cross into the network (a symbolic link): only that check is lost.
        using var release = new ManualResetEventSlim();
        var sut = new PathResolver(
            path => !path.Contains("Link", StringComparison.Ordinal) || release.Wait(TimeSpan.FromSeconds(30)),
            PathResolver.DefaultMaxAbandoned,
            isRemoteDrive: _ => false);
        try
        {
            Assert.False(await sut.ExistsAsync(@"C:\Link\Projets", TimeSpan.FromMilliseconds(100)));
            Assert.True(await sut.ExistsAsync(@"C:\Users\Me", TimeSpan.FromSeconds(2)));
        }
        finally
        {
            release.Set();
        }

        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
    }

    [Fact]
    public async Task ExistsAsync_treats_a_drive_whose_type_question_hangs_as_a_hung_share()
    {
        using var release = new ManualResetEventSlim();
        var sut = new PathResolver(
            _ => true,
            PathResolver.DefaultMaxAbandoned,
            isRemoteDrive: root => !root.StartsWith("P", StringComparison.OrdinalIgnoreCase) || release.Wait(TimeSpan.FromSeconds(30)));
        try
        {
            Assert.False(await sut.ExistsAsync(@"P:\Projets", TimeSpan.FromMilliseconds(100)));

            var refused = sut.ExistsAsync(@"P:\Archives", TimeSpan.FromSeconds(2));
            Assert.True(refused.IsCompleted);
            Assert.False(await refused);
            Assert.True(await sut.ExistsAsync(@"C:\Users\Me", TimeSpan.FromSeconds(2)));
        }
        finally
        {
            release.Set();
        }

        Assert.True(WaitUntil(() => sut.AbandonedCount == 0, EventTimeout));
    }

    [Fact]
    public async Task ExistsAsync_reports_a_failing_check_as_missing()
    {
        var sut = new PathResolver(_ => throw new IOException("boom"), PathResolver.DefaultMaxAbandoned);

        Assert.False(await sut.ExistsAsync(@"C:\Anything", TimeSpan.FromSeconds(2)));
        Assert.Equal(0, sut.AbandonedCount);
    }

    // ---- Autostart ----

    [Fact]
    public void Autostart_roundtrip()
    {
        var runKey = _testKey + @"\Run";
        var sut = new Autostart(runKey, "FolderSwitch");
        const string exe = @"C:\Users\Test\AppData\Local\FolderSwitch\current\FolderSwitch.exe";

        Assert.False(sut.IsEnabled);

        sut.Set(true, exe);
        Assert.True(sut.IsEnabled);
        Assert.Equal("\"" + exe + "\"", ReadValue(runKey, "FolderSwitch"));
        Assert.Equal(RegistryValueKind.String, ValueKind(runKey, "FolderSwitch"));

        // Enabling again replaces the path.
        sut.Set(true, @"D:\Apps\FolderSwitch.exe");
        Assert.Equal("\"D:\\Apps\\FolderSwitch.exe\"", ReadValue(runKey, "FolderSwitch"));

        sut.Set(false, exe);
        Assert.False(sut.IsEnabled);
        Assert.Null(ReadValue(runKey, "FolderSwitch"));

        // Disabling when already disabled does nothing.
        sut.Set(false, exe);
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public void Autostart_quotes_the_path_once()
    {
        var runKey = _testKey + @"\Run";
        var sut = new Autostart(runKey, "FolderSwitch");

        sut.Set(true, "\"C:\\Program Files\\FolderSwitch\\FolderSwitch.exe\"");

        Assert.Equal("\"C:\\Program Files\\FolderSwitch\\FolderSwitch.exe\"", ReadValue(runKey, "FolderSwitch"));
    }

    [Fact]
    public void Autostart_touches_only_its_own_value()
    {
        var runKey = _testKey + @"\Run";
        using (var key = Registry.CurrentUser.CreateSubKey(runKey))
        {
            key.SetValue("OtherApp", "\"C:\\Other\\Other.exe\"");
        }

        var sut = new Autostart(runKey, "FolderSwitch");
        Assert.False(sut.IsEnabled);
        sut.Set(true, @"C:\FolderSwitch.exe");
        sut.Set(false, @"C:\FolderSwitch.exe");

        Assert.Equal("\"C:\\Other\\Other.exe\"", ReadValue(runKey, "OtherApp"));
    }

    [Fact]
    public void Autostart_disabling_without_a_key_does_nothing()
    {
        var runKey = _testKey + @"\Missing";
        var sut = new Autostart(runKey, "FolderSwitch");

        sut.Set(false, @"C:\FolderSwitch.exe");

        Assert.False(sut.IsEnabled);
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        Assert.Null(key);
    }

    [Fact]
    public void Autostart_refuses_a_blank_path()
    {
        var sut = new Autostart(_testKey + @"\Run", "FolderSwitch");

        Assert.ThrowsAny<ArgumentException>(() => sut.Set(true, ""));
        Assert.ThrowsAny<ArgumentException>(() => sut.Set(true, "\"\""));
        Assert.False(sut.IsEnabled);
    }

    // ---- SystemInfo ----

    [Fact]
    public void Generation_matches_build_number()
    {
        using var sut = new SystemInfo(null);

        var expected = Environment.OSVersion.Version.Build >= 22000 ? WindowsGeneration.Windows11 : WindowsGeneration.Windows10;
        Assert.Equal(expected, sut.Generation);
    }

    [Fact]
    public void UiCultureName_is_the_display_language()
    {
        using var sut = new SystemInfo(null);

        var name = sut.UiCultureName;

        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(name, CultureInfo.GetCultureInfo(name).Name, ignoreCase: true);

        // What .NET took as the UI culture at start-up, which this test process never changed.
        Assert.Equal(CultureInfo.CurrentUICulture.Name, name, ignoreCase: true);

        // Not affected by the application changing its own UI culture.
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(original.Name == "pl-PL" ? "nl-NL" : "pl-PL");
            Assert.Equal(name, sut.UiCultureName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void AppsUseDarkTheme_reads_the_user_setting()
    {
        using var sut = new SystemInfo(null);

        // Read only: the user's theme is never written.
        using var key = Registry.CurrentUser.OpenSubKey(SystemInfo.PersonalizeKeyPath);
        var expected = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        Assert.Equal(expected, sut.AppsUseDarkTheme);
        Assert.True(sut.IsWatching);
    }

    [Fact]
    public void ThemeChanged_not_raised_without_change()
    {
        var personalize = CreatePersonalizeKey(appsUseLightTheme: 1);
        using var sut = new SystemInfo(null, personalize);
        var raised = 0;
        sut.ThemeChanged += (_, _) => Interlocked.Increment(ref raised);
        Assert.True(sut.IsWatching);
        Assert.False(sut.AppsUseDarkTheme);

        // Another value of the key changed: notified, not raised.
        WriteAndWaitForNotification(sut, personalize, "ColorPrevalence", 1);
        WriteAndWaitForNotification(sut, personalize, "ColorPrevalence", 0);

        // The same value written again (Windows does not even notify that).
        WriteSameValueAndSettle(sut, personalize, "AppsUseLightTheme", 1);

        Assert.Equal(0, raised);
        Assert.False(sut.AppsUseDarkTheme);
    }

    [Fact]
    public void ThemeChanged_raised_once_per_change()
    {
        var personalize = CreatePersonalizeKey(appsUseLightTheme: 1);
        using var sut = new SystemInfo(null, personalize);
        var raised = 0;
        var darkWhenRaised = new ConcurrentQueue<bool>();
        var senders = new ConcurrentQueue<object?>();

        // No assertion in the handler: it runs on a thread-pool thread.
        sut.ThemeChanged += (sender, _) =>
        {
            senders.Enqueue(sender);
            darkWhenRaised.Enqueue(sut.AppsUseDarkTheme);
            Interlocked.Increment(ref raised);
        };

        WriteAndWaitForNotification(sut, personalize, "AppsUseLightTheme", 0);
        Assert.Equal(1, raised);
        Assert.True(sut.AppsUseDarkTheme);

        WriteSameValueAndSettle(sut, personalize, "AppsUseLightTheme", 0);
        Assert.Equal(1, raised);

        WriteAndWaitForNotification(sut, personalize, "AppsUseLightTheme", 1);
        Assert.Equal(2, raised);
        Assert.False(sut.AppsUseDarkTheme);
        Assert.Equal(new[] { true, false }, darkWhenRaised);
        Assert.All(senders, sender => Assert.Same(sut, sender));
    }

    [Fact]
    public void ThemeChanged_follows_a_value_removed_and_a_key_created_later()
    {
        // The parent exists, Personalize does not yet: light, and watched all the same.
        var personalize = _testKey + @"\Themes\Personalize";
        Registry.CurrentUser.CreateSubKey(_testKey + @"\Themes").Dispose();
        using var sut = new SystemInfo(null, personalize);
        var raised = 0;
        sut.ThemeChanged += (_, _) => Interlocked.Increment(ref raised);
        Assert.True(sut.IsWatching);
        Assert.False(sut.AppsUseDarkTheme);

        WriteAndWaitForNotification(sut, personalize, "AppsUseLightTheme", 0);
        Assert.Equal(1, raised);
        Assert.True(sut.AppsUseDarkTheme);

        // No value: light again.
        var before = sut.NotificationCount;
        using (var key = Registry.CurrentUser.OpenSubKey(personalize, writable: true)!)
        {
            key.DeleteValue("AppsUseLightTheme");
        }

        Assert.True(WaitUntil(() => sut.NotificationCount > before, EventTimeout));
        Assert.Equal(2, raised);
        Assert.False(sut.AppsUseDarkTheme);
    }

    [Fact]
    public void ThemeChanged_is_posted_to_the_context()
    {
        var personalize = CreatePersonalizeKey(appsUseLightTheme: 1);
        var context = new QueueContext();
        using var sut = new SystemInfo(context, personalize);
        var raised = 0;
        sut.ThemeChanged += (_, _) => Interlocked.Increment(ref raised);

        WriteAndWaitForNotification(sut, personalize, "AppsUseLightTheme", 0);

        Assert.Equal(0, raised);
        Assert.Equal(1, context.RunAll());
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ThemeChanged_not_raised_after_dispose()
    {
        var personalize = CreatePersonalizeKey(appsUseLightTheme: 1);
        var context = new QueueContext();
        var sut = new SystemInfo(context, personalize);
        var raised = 0;
        sut.ThemeChanged += (_, _) => Interlocked.Increment(ref raised);

        // Posted before Dispose, run after: dropped.
        WriteAndWaitForNotification(sut, personalize, "AppsUseLightTheme", 0);
        sut.Dispose();
        Assert.Equal(1, context.RunAll());
        Assert.Equal(0, raised);
        Assert.False(sut.IsWatching);

        // Changed after Dispose: not even handled.
        var before = sut.NotificationCount;
        WriteValue(personalize, "AppsUseLightTheme", 1);
        Thread.Sleep(300);
        Assert.Equal(before, sut.NotificationCount);
        Assert.Equal(0, context.RunAll());
        sut.Dispose();
    }

    [Fact]
    public void SystemInfo_without_its_key_still_answers()
    {
        using var sut = new SystemInfo(null, _testKey + @"\Missing\Personalize");

        Assert.False(sut.IsWatching);
        Assert.False(sut.AppsUseDarkTheme);
    }

    // ---- SingleInstance ----

    [Fact]
    public void SingleInstance_second_acquire_signals_first()
    {
        var name = UniqueName();
        using var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);
        using var requested = new SemaphoreSlim(0);
        var senders = new ConcurrentQueue<object?>();

        // No assertion in the handler: it runs on the instance's waiting thread.
        first.ActivationRequested += (sender, _) =>
        {
            senders.Enqueue(sender);
            requested.Release();
        };

        Assert.Null(SingleInstance.TryAcquire(name));
        Assert.True(requested.Wait(EventTimeout));
        Assert.Same(first, Assert.Single(senders));

        // Each later instance signals again.
        Assert.Null(SingleInstance.TryAcquire(name));
        Assert.True(requested.Wait(EventTimeout));
        Assert.False(requested.Wait(200));
    }

    [Fact]
    public void SingleInstance_is_free_again_after_dispose()
    {
        var name = UniqueName();
        var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);
        first.Dispose();
        first.Dispose();

        using var next = SingleInstance.TryAcquire(name);
        Assert.NotNull(next);
    }

    [Fact]
    public void SingleInstance_uses_the_named_objects()
    {
        Assert.Equal(@"Local\FolderSwitch.SingleInstance", SingleInstance.MutexName("FolderSwitch"));
        Assert.Equal(@"Local\FolderSwitch.Activate", SingleInstance.ActivationEventName("FolderSwitch"));

        var name = UniqueName();
        Assert.False(Mutex.TryOpenExisting(SingleInstance.MutexName(name), out _));
        using (var instance = SingleInstance.TryAcquire(name))
        {
            Assert.NotNull(instance);
            using var requested = new SemaphoreSlim(0);
            instance.ActivationRequested += (_, _) => requested.Release();

            // What another process sees: the mutex is there and owned; signaling the event activates.
            Assert.True(Mutex.TryOpenExisting(SingleInstance.MutexName(name), out var mutex));
            using (mutex)
            {
                var owned = true;
                var other = new Thread(() => owned = mutex.WaitOne(0));
                other.Start();
                other.Join();
                Assert.False(owned);
            }

            using var activate = EventWaitHandle.OpenExisting(SingleInstance.ActivationEventName(name));
            activate.Set();
            Assert.True(requested.Wait(EventTimeout));
        }

        // Released and closed: gone.
        Assert.False(Mutex.TryOpenExisting(SingleInstance.MutexName(name), out _));
    }

    [Fact]
    public void SingleInstance_takes_over_an_abandoned_mutex()
    {
        var name = UniqueName();
        using var held = new ManualResetEventSlim();
        using var end = new ManualResetEventSlim();
        Mutex? mutex = null;
        var crashed = new Thread(() =>
        {
            // Owned by a thread that ends without releasing it, as a crashed instance would.
            mutex = new Mutex(true, SingleInstance.MutexName(name));
            held.Set();
            end.Wait();
        });
        crashed.Start();
        Assert.True(held.Wait(EventTimeout));
        try
        {
            Assert.Null(SingleInstance.TryAcquire(name));
            end.Set();
            crashed.Join();

            using var instance = SingleInstance.TryAcquire(name);
            Assert.NotNull(instance);
        }
        finally
        {
            end.Set();
            crashed.Join();
            mutex?.Dispose();
        }
    }

    [Fact]
    public void SingleInstance_posts_to_the_context()
    {
        var name = UniqueName();
        var context = new QueueContext();
        using var first = SingleInstance.TryAcquire(name, context);
        Assert.NotNull(first);
        var raised = 0;
        first.ActivationRequested += (_, _) => Interlocked.Increment(ref raised);

        Assert.Null(SingleInstance.TryAcquire(name));

        Assert.True(WaitUntil(() => context.Count == 1, EventTimeout));
        Assert.Equal(0, raised);
        Assert.Equal(1, context.RunAll());
        Assert.Equal(1, raised);

        // Posted, then disposed: dropped.
        Assert.Null(SingleInstance.TryAcquire(name));
        Assert.True(WaitUntil(() => context.Count == 1, EventTimeout));
        first.Dispose();
        Assert.Equal(1, context.RunAll());
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SingleInstance_can_be_disposed_from_its_handler()
    {
        var name = UniqueName();
        var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);
        using var disposed = new ManualResetEventSlim();
        first.ActivationRequested += (_, _) =>
        {
            first.Dispose();
            disposed.Set();
        };

        Assert.Null(SingleInstance.TryAcquire(name));
        Assert.True(disposed.Wait(EventTimeout));

        // Released once the handler has returned.
        SingleInstance? next = null;
        Assert.True(WaitUntil(() => (next = SingleInstance.TryAcquire(name)) is not null, EventTimeout));
        next!.Dispose();
    }

    [Fact]
    public void SingleInstance_refuses_a_bad_name()
    {
        Assert.ThrowsAny<ArgumentException>(() => SingleInstance.TryAcquire(""));
        Assert.ThrowsAny<ArgumentException>(() => SingleInstance.TryAcquire(@"Global\FolderSwitch"));
    }

    // ---- Helpers ----

    private static Item PathItem(string path) => new() { Kind = ItemKind.Folder, Path = path };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string UniqueName() => "FolderSwitch.Tests." + Guid.NewGuid().ToString("N");

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (!condition())
        {
            if (Stopwatch.GetTimestamp() > deadline)
            {
                return condition();
            }

            Thread.Sleep(10);
        }

        return true;
    }

    private static object? ReadValue(string keyPath, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(name);
    }

    private static RegistryValueKind ValueKind(string keyPath, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath)!;
        return key.GetValueKind(name);
    }

    private static void WriteValue(string keyPath, string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    /// <summary>Writes a DWORD and waits until the instance has handled the change notification.</summary>
    private static void WriteAndWaitForNotification(SystemInfo sut, string keyPath, string name, int value)
    {
        var before = sut.NotificationCount;
        WriteValue(keyPath, name, value);
        Assert.True(WaitUntil(() => sut.NotificationCount > before, EventTimeout), $"No notification for {name} = {value}.");

        // A write may give more than one notification: let them all be handled.
        Thread.Sleep(100);
    }

    /// <summary>
    /// Writes a DWORD that already holds <paramref name="value"/>, then changes another value and waits
    /// for that notification: notifications are handled one at a time, in order, so one the first
    /// write gave (Windows gives none) would have been handled by then.
    /// </summary>
    private static void WriteSameValueAndSettle(SystemInfo sut, string keyPath, string name, int value)
    {
        Assert.Equal(value, ReadValue(keyPath, name));
        WriteValue(keyPath, name, value);
        WriteAndWaitForNotification(sut, keyPath, "FolderSwitchTestMarker", Environment.TickCount);
    }

    /// <summary>A test Personalize key (and its parent) holding <c>AppsUseLightTheme</c>.</summary>
    private string CreatePersonalizeKey(int appsUseLightTheme)
    {
        var personalize = _testKey + @"\Themes\Personalize";
        WriteValue(personalize, "AppsUseLightTheme", appsUseLightTheme);
        return personalize;
    }

    /// <summary>A context that queues what is posted to it, run on demand.</summary>
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public int Count => _posted.Count;

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public int RunAll()
        {
            var count = 0;
            while (_posted.TryDequeue(out var posted))
            {
                posted.Callback(posted.State);
                count++;
            }

            return count;
        }
    }
}
