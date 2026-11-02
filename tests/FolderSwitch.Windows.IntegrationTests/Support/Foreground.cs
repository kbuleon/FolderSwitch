// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// Brings a window to the foreground from the test process, which Windows normally refuses to a
/// background process. Workaround, without any keystroke: the calling thread attaches its input
/// state to the threads of the current foreground window and of the target for the duration of
/// <c>SetForegroundWindow</c> (the system then treats the call as coming from the foreground), then
/// detaches. Retried until the window is the foreground one or the timeout expires.
/// </summary>
internal static class Foreground
{
    private const int SwRestore = 9;

    public static bool TryBring(nint window, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (GetForegroundWindow() == window)
            {
                return true;
            }

            Attempt(window);
            for (var i = 0; i < 10; i++)
            {
                if (GetForegroundWindow() == window)
                {
                    return true;
                }

                Thread.Sleep(20);
            }
        }
        while (DateTime.UtcNow < deadline);

        return GetForegroundWindow() == window;
    }

    /// <summary>Brings <paramref name="window"/> to the foreground or fails the test.</summary>
    public static void Bring(nint window)
    {
        Assert.True(
            TryBring(window, TimeSpan.FromSeconds(5)),
            $"Could not bring window 0x{window:X} to the foreground (foreground: 0x{GetForegroundWindow():X}).");
    }

    public static nint Current => GetForegroundWindow();

    private static void Attempt(nint window)
    {
        var self = GetCurrentThreadId();
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var targetThread = GetWindowThreadProcessId(window, out _);
        var attached = new List<uint>();
        try
        {
            foreach (var thread in new[] { foregroundThread, targetThread }.Distinct())
            {
                if (thread != 0 && thread != self && AttachThreadInput(self, thread, true))
                {
                    attached.Add(thread);
                }
            }

            if (IsIconic(window))
            {
                ShowWindow(window, SwRestore);
            }

            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            foreach (var thread in attached)
            {
                AttachThreadInput(self, thread, false);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);
}
