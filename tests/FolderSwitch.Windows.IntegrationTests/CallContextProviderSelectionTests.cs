// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.SystemServices;
using Xunit.Abstractions;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// The file selected in an Explorer window, read by <see cref="CallContextProvider.TryReadSelection"/>: on the real
/// desktop session, Explorer windows (or tabs) open on a temporary folder, their selection set through the view
/// (<see cref="ExplorerViewSelection"/>: no keystroke, no click), closed afterwards, also when an assertion fails. The
/// tests of the read's rules (no window, overrun) open no window.
/// </summary>
/// <remarks>
/// A read is retried for a short while until it gives an answer: the first reads of the lookup thread may run cold, or
/// wait for its warm-up.
/// </remarks>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class CallContextProviderSelectionTests(ITestOutputHelper output) : IDisposable
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(5);

    /// <summary><c>HWND_MESSAGE</c>: the parent of a message-only window.</summary>
    private static readonly nint MessageOnlyParent = -3;

    private readonly CallContextProvider _sut = new();
    private readonly TempFolder _folder = new();
    private readonly TempFolder _elsewhere = new();

    public void Dispose()
    {
        _sut.Dispose();
        _folder.Dispose();
        _elsewhere.Dispose();
    }

    // ---- In an Explorer window ----

    [Fact]
    public void One_selected_file_gives_its_path()
    {
        var file = CreateFile(_folder, "one.txt");
        CreateFile(_folder, "two.txt");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, file);

        AssertFile(file, ReadSelection(explorer));
    }

    [Fact]
    public void One_selected_folder_gives_no_file()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder.Path, "sub")).FullName;
        CreateFile(_folder, "one.txt");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, folder);

        Assert.Equal(ExplorerSelection.NoFile, ReadSelection(explorer));
    }

    [Fact]
    public void Two_selected_files_give_no_file()
    {
        var one = CreateFile(_folder, "one.txt");
        var two = CreateFile(_folder, "two.txt");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, one, two);

        Assert.Equal(ExplorerSelection.NoFile, ReadSelection(explorer));
    }

    [Fact]
    public void Nothing_selected_gives_no_file()
    {
        var file = CreateFile(_folder, "one.txt");
        using var explorer = OpenFolder();

        // Selected first, then cleared: the read sees the cleared selection, not a view that has not settled yet.
        ExplorerViewSelection.SelectOnly(explorer, file);
        AssertFile(file, ReadSelection(explorer));
        ExplorerViewSelection.Clear(explorer, file);

        Assert.Equal(ExplorerSelection.NoFile, ReadSelection(explorer));
    }

    [Fact]
    public void A_selected_zip_gives_its_path()
    {
        // Both a folder and a file for the shell.
        var zip = Path.Combine(_folder.Path, "archive.zip");
        ZipFile.Open(zip, ZipArchiveMode.Create).Dispose();
        CreateFile(_folder, "one.txt");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, zip);

        AssertFile(zip, ReadSelection(explorer));
    }

    [Fact]
    public void A_selected_shortcut_to_a_file_gives_the_target_path()
    {
        var target = CreateFile(_elsewhere, "target.txt");
        var link = Path.Combine(_folder.Path, "target.lnk");
        Shortcut.Create(link, target);
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, link);

        AssertFile(target, ReadSelection(explorer));
    }

    [Fact]
    public void A_selected_shortcut_to_a_folder_gives_no_file()
    {
        var link = Path.Combine(_folder.Path, "folder.lnk");
        Shortcut.Create(link, _elsewhere.Path);
        CreateFile(_folder, "one.txt");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, link);

        Assert.Equal(ExplorerSelection.NoFile, ReadSelection(explorer));
    }

    [Fact]
    public void A_selected_shortcut_to_a_missing_file_gives_no_file()
    {
        var target = CreateFile(_elsewhere, "gone.txt");
        var link = Path.Combine(_folder.Path, "gone.lnk");
        Shortcut.Create(link, target);
        File.Delete(target);
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, link);

        Assert.Equal(ExplorerSelection.NoFile, ReadSelection(explorer));
    }

    [Fact]
    public void A_selected_shortcut_that_does_not_load_gives_no_file()
    {
        // A damaged .lnk: a shell link whose target cannot be read, not a plain file.
        var link = Path.Combine(_folder.Path, "damaged.lnk");
        File.WriteAllText(link, "not a shell link");
        CreateFile(_folder, "one.txt");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, link);

        Assert.Equal(ExplorerSelection.NoFile, ReadSelection(explorer));
    }

    [Fact]
    public void A_selected_Internet_shortcut_is_a_plain_file()
    {
        var url = Path.Combine(_folder.Path, "site.url");
        File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com/\r\n");
        using var explorer = OpenFolder();

        ExplorerViewSelection.SelectOnly(explorer, url);

        AssertFile(url, ReadSelection(explorer));
    }

    // ---- The read's rules, without Explorer ----

    [Fact]
    public void No_window_or_a_window_that_is_gone_gives_null_at_once()
    {
        var calls = 0;
        nint asked = 0;
        using var sut = new CallContextProvider(
            CallContextProvider.DefaultBudget,
            (_, _) => null,
            selectionLookup: window =>
            {
                Interlocked.Increment(ref calls);
                asked = window;
                return ExplorerSelection.NoFile;
            });

        var gone = CreateMessageOnlyWindow();
        Assert.True(DestroyWindow(gone));

        var watch = Stopwatch.StartNew();
        Assert.Null(sut.TryReadSelection(0));
        Assert.Null(sut.TryReadSelection(gone));
        watch.Stop();
        output.WriteLine($"two reads without a window: {watch.Elapsed.TotalMilliseconds:F1} ms");

        Assert.Equal(0, Volatile.Read(ref calls));
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(50), $"{watch.Elapsed.TotalMilliseconds:F1} ms");

        // A window that is there is looked up, and the lookup's answer given.
        var window = CreateMessageOnlyWindow();
        try
        {
            Assert.Equal(ExplorerSelection.NoFile, sut.TryReadSelection(window));
            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.Equal(window, asked);
        }
        finally
        {
            DestroyWindow(window);
        }
    }

    [Fact]
    public void A_selection_read_that_overruns_gives_null_within_its_budget_and_suspends_the_lookup()
    {
        using var release = new ManualResetEventSlim();
        var folderCalls = 0;
        var answer = new ExplorerSelection(@"C:\released.txt");
        using var sut = new CallContextProvider(
            CallContextProvider.DefaultBudget,
            (_, _) =>
            {
                Interlocked.Increment(ref folderCalls);
                return "folder";
            },
            readBudget: TimeSpan.FromMilliseconds(200),
            selectionLookup: _ =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                return answer;
            });

        var window = CreateMessageOnlyWindow();
        try
        {
            var watch = Stopwatch.StartNew();
            var selection = sut.TryReadSelection(window);
            watch.Stop();
            output.WriteLine($"selection read that overran: {watch.Elapsed.TotalMilliseconds:F1} ms");

            Assert.Null(selection);
            Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(400), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
            Assert.True(sut.IsLookupSuspended);

            // The lookup thread is the folder reads' too: while the abandoned lookup runs, they do not start another.
            Assert.Null(sut.TryRead(CallContextKind.Explorer, window));
            Assert.Equal(0, Volatile.Read(ref folderCalls));

            // Once it has returned, the next read runs on a new thread.
            release.Set();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (sut.IsLookupSuspended && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }

            Assert.False(sut.IsLookupSuspended);
            Assert.Equal(answer, sut.TryReadSelection(window));
            Assert.Equal(new FolderLocation("folder", null), sut.TryRead(CallContextKind.Explorer, window));
        }
        finally
        {
            DestroyWindow(window);
        }
    }

    [Fact]
    public void A_disposed_provider_gives_null()
    {
        var calls = 0;
        var sut = new CallContextProvider(
            CallContextProvider.DefaultBudget,
            (_, _) => null,
            selectionLookup: _ =>
            {
                Interlocked.Increment(ref calls);
                return ExplorerSelection.NoFile;
            });
        sut.Dispose();

        var window = CreateMessageOnlyWindow();
        try
        {
            Assert.Null(sut.TryReadSelection(window));
            Assert.Equal(0, Volatile.Read(ref calls));
        }
        finally
        {
            DestroyWindow(window);
        }
    }

    // ---- Helpers ----

    private static string CreateFile(TempFolder folder, string name)
    {
        var path = Path.Combine(folder.Path, name);
        File.WriteAllText(path, name);
        return path;
    }

    /// <summary>Opens <see cref="_folder"/>, whose items are all created already: the view lists them as it opens.</summary>
    private OpenedExplorer OpenFolder()
    {
        var explorer = OpenedExplorer.Open(_folder.Path, _folder.Path);
        output.WriteLine($"Explorer window 0x{explorer.Window:X}, tab 0x{explorer.Tab:X}, new window: {explorer.IsNewWindow}");
        return explorer;
    }

    /// <summary>Reads the selection until a read gives an answer or <see cref="SettleTimeout"/> expires; the last read.</summary>
    private ExplorerSelection? ReadSelection(OpenedExplorer explorer)
    {
        var deadline = DateTime.UtcNow + SettleTimeout;
        var attempts = 0;
        while (true)
        {
            var watch = Stopwatch.StartNew();
            var selection = _sut.TryReadSelection(explorer.Window);
            watch.Stop();
            attempts++;
            output.WriteLine($"read {attempts}: {watch.Elapsed.TotalMilliseconds:F1} ms, {selection?.ToString() ?? "null"}");

            if (selection is not null || DateTime.UtcNow > deadline)
            {
                return selection;
            }

            Thread.Sleep(200);
        }
    }

    private static void AssertFile(string expected, ExplorerSelection? selection)
    {
        Assert.NotNull(selection);
        Assert.Equal(expected, selection.FilePath, ignoreCase: true);
    }

    /// <summary>A message-only window of this thread: a real window handle, never shown.</summary>
    private static nint CreateMessageOnlyWindow()
    {
        var window = CreateWindowEx(0, "STATIC", null, 0, 0, 0, 0, 0, MessageOnlyParent, 0, 0, 0);
        Assert.NotEqual(0, window);
        return window;
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateWindowEx(
        uint exStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
}
