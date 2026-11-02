// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using System.Runtime.InteropServices;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Runs on the real desktop session: a message box shown by a thread of the test process is brought
/// over an Explorer window that holds the foreground, as FolderSwitch's window is brought over the
/// application the user works in. Every window opened is closed, also when an assertion fails. No
/// keystroke is sent.
/// </summary>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class ForegroundActivatorTests
{
    private const int SwMinimize = 6;

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(3);

    private readonly ForegroundActivator _sut = new();

    [Fact]
    public void Activate_brings_a_window_over_another_process()
    {
        using var folder = new TempFolder();
        using var explorer = OpenedExplorer.Open(folder.Path, folder.Path);
        using var box = MessageBoxScope.Show();
        Foreground.Bring(explorer.Window);

        _sut.Activate(box.Window);

        Assert.True(
            WaitUntil(() => Foreground.Current == box.Window, SettleTimeout),
            $"Foreground: 0x{Foreground.Current:X}, expected 0x{box.Window:X}.");
    }

    [Fact]
    public void Activate_restores_a_minimized_window()
    {
        using var folder = new TempFolder();
        using var explorer = OpenedExplorer.Open(folder.Path, folder.Path);
        using var box = MessageBoxScope.Show();
        ShowWindow(box.Window, SwMinimize);
        Assert.True(WaitUntil(() => IsIconic(box.Window), SettleTimeout), "The message box was not minimized.");
        Foreground.Bring(explorer.Window);

        _sut.Activate(box.Window);

        Assert.True(WaitUntil(() => !IsIconic(box.Window), SettleTimeout), "The message box is still minimized.");
        Assert.True(
            WaitUntil(() => Foreground.Current == box.Window, SettleTimeout),
            $"Foreground: 0x{Foreground.Current:X}, expected 0x{box.Window:X}.");
    }

    [Fact]
    public void Activate_ignores_what_is_not_a_window()
    {
        // Nothing thrown, nothing done (-1 is HWND_BROADCAST, not a window either).
        _sut.Activate(0);
        _sut.Activate(0x7FFF_FFF0);
        _sut.Activate(-1);
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                return condition();
            }

            Thread.Sleep(20);
        }

        return true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);
}
