// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Windows.Threading;

namespace DialogHost;

/// <summary>
/// Watches the dialog while it is shown, from background threads: reports <c>READY</c> once the
/// dialog window (a visible top-level <c>#32770</c> owned by the host window) appears, and cancels
/// the dialog when standard input receives <c>quit</c> or is closed. <c>hang &lt;ms&gt;</c> blocks the
/// UI thread (hence the dialog) for that long, between <c>HUNG</c> and <c>AWAKE</c> lines: a dialog
/// that does not answer messages. <c>slow &lt;ms&gt;</c> holds the next folder-reading message the dialog
/// takes (see <see cref="MessageDelay"/>). <c>drop</c> / <c>drop reset</c> drops the next validation,
/// <c>slow-ok &lt;ms&gt;</c> holds then defers the next OK (see <see cref="ValidationDrop"/>).
/// </summary>
/// <remarks>Must be created on the UI thread, which is the thread that shows the dialog.</remarks>
internal sealed class DialogSession(nint owner)
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan QuitGrace = TimeSpan.FromSeconds(5);

    private readonly uint _uiThreadId = Native.GetCurrentThreadId();
    private readonly CancellationTokenSource _stop = new();
    private nint _dialog;
    private volatile bool _quitRequested;

    public void Start()
    {
        new Thread(WatchForDialog) { IsBackground = true, Name = "DialogHost watcher" }.Start();
        if (Console.IsInputRedirected)
        {
            new Thread(WatchInput) { IsBackground = true, Name = "DialogHost input" }.Start();
        }
    }

    public void Stop() => _stop.Cancel();

    private void WatchForDialog()
    {
        while (!_stop.IsCancellationRequested)
        {
            var dialog = FindDialog();
            if (dialog != 0)
            {
                Interlocked.Exchange(ref _dialog, dialog);
                HostOutput.Line("READY " + dialog.ToString("X"));
                if (_quitRequested)
                {
                    Native.PostMessage(dialog, Native.WM_CLOSE, 0, 0);
                }

                return;
            }

            _stop.Token.WaitHandle.WaitOne(PollInterval);
        }
    }

    private nint FindDialog()
    {
        nint found = 0;
        Native.EnumThreadWindows(
            _uiThreadId,
            (hwnd, _) =>
            {
                if (Native.IsWindowVisible(hwnd)
                    && Native.GetWindow(hwnd, Native.GW_OWNER) == owner
                    && Native.ClassName(hwnd) == "#32770")
                {
                    found = hwnd;
                    return false;
                }

                return true;
            },
            0);
        return found;
    }

    private void WatchInput()
    {
        try
        {
            while (Console.In.ReadLine() is { } line)
            {
                var command = line.Trim();
                if (string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (command.StartsWith("slow ", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(command[5..], out var delay))
                {
                    var dialog = Interlocked.CompareExchange(ref _dialog, 0, 0);
                    _dispatcher.BeginInvoke(() => MessageDelay.Arm(dialog, delay));
                }

                if (string.Equals(command, "drop", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command, "drop reset", StringComparison.OrdinalIgnoreCase))
                {
                    var dialog = Interlocked.CompareExchange(ref _dialog, 0, 0);
                    var reset = command.Length > "drop".Length;
                    _dispatcher.BeginInvoke(() => ValidationDrop.Arm(dialog, reset));
                }

                if (command.StartsWith("slow-ok ", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(command["slow-ok ".Length..], out var okDelay))
                {
                    var dialog = Interlocked.CompareExchange(ref _dialog, 0, 0);
                    _dispatcher.BeginInvoke(() => ValidationDrop.ArmSlowOk(dialog, okDelay));
                }

                if (command.StartsWith("hang ", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(command[5..], out var milliseconds))
                {
                    // Dispatched by the dialog's own message loop, like any message of the thread.
                    _dispatcher.BeginInvoke(() =>
                    {
                        HostOutput.Line("HUNG");
                        Thread.Sleep(milliseconds);
                        HostOutput.Line("AWAKE");
                    });
                }
            }
        }
        catch (IOException)
        {
            // Treated like a closed input.
        }

        RequestQuit();
    }

    private void RequestQuit()
    {
        _quitRequested = true;
        var dialog = Interlocked.CompareExchange(ref _dialog, 0, 0);
        if (dialog != 0)
        {
            Native.PostMessage(dialog, Native.WM_CLOSE, 0, 0);
        }

        // Last resort if the dialog does not close (or never appears).
        if (!_stop.Token.WaitHandle.WaitOne(QuitGrace))
        {
            HostOutput.Line("ERROR the dialog did not close after quit");
            Environment.Exit(2);
        }
    }
}
