using FolderSwitch.Core.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// Brings a window to the foreground: <c>SetForegroundWindow</c>, and when Windows refuses it (the
/// calling process is not the foreground one, as when a global hotkey fires over another
/// application), the same call with the calling thread's input attached to the foreground thread's.
/// </summary>
/// <remarks>
/// <para>
/// A minimized window is restored first: synchronously when the calling thread owns it, otherwise
/// with <c>ShowWindowAsync</c>, which does not wait for the window's thread. A hidden window is not
/// shown: that is the caller's job. No keystroke is ever synthesized.
/// </para>
/// <para>
/// <b>Fallback.</b> <c>AttachThreadInput</c> with the thread of the current foreground window makes the
/// system treat the calling thread as part of the foreground for the duration of
/// <c>SetForegroundWindow</c> and <c>BringWindowToTop</c>; it is detached right after, whatever happens.
/// It is skipped when the foreground window does not answer (<c>IsHungAppWindow</c>): attached to a
/// hung thread, the calling thread could wait on it. Then the plain calls are made again, which at
/// worst flash the window's taskbar button.
/// </para>
/// <para>Best effort: nothing is thrown, and nothing is reported.</para>
/// </remarks>
public sealed class ForegroundActivator : IForegroundActivator
{
    /// <inheritdoc/>
    public unsafe bool IsOwnProcessInForeground()
    {
        try
        {
            var foreground = PInvoke.GetForegroundWindow();
            if (foreground.IsNull)
            {
                return false;
            }

            uint processId;
            return PInvoke.GetWindowThreadProcessId(foreground, &processId) != 0 && processId == (uint)Environment.ProcessId;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public void Activate(nint hwnd)
    {
        try
        {
            var window = (HWND)hwnd;
            if (hwnd == 0 || !PInvoke.IsWindow(window))
            {
                return;
            }

            Restore(window);
            if (PInvoke.SetForegroundWindow(window) && IsForeground(window))
            {
                return;
            }

            ActivateAttached(window);
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    private static void Restore(HWND window)
    {
        if (!PInvoke.IsIconic(window))
        {
            return;
        }

        if (PInvoke.GetWindowThreadProcessId(window) == PInvoke.GetCurrentThreadId())
        {
            _ = PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_RESTORE);
        }
        else
        {
            _ = PInvoke.ShowWindowAsync(window, SHOW_WINDOW_CMD.SW_RESTORE);
        }
    }

    /// <summary>The window, or a window it owns (a dialog it opened), is the foreground one.</summary>
    private static bool IsForeground(HWND window)
    {
        var foreground = PInvoke.GetForegroundWindow();
        return foreground == window
            || (!foreground.IsNull && PInvoke.GetAncestor(foreground, GET_ANCESTOR_FLAGS.GA_ROOTOWNER) == window);
    }

    private static void ActivateAttached(HWND window)
    {
        var foreground = PInvoke.GetForegroundWindow();
        var self = PInvoke.GetCurrentThreadId();
        var foregroundThread = foreground.IsNull ? 0 : PInvoke.GetWindowThreadProcessId(foreground);
        var attached = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != self && !PInvoke.IsHungAppWindow(foreground))
            {
                attached = PInvoke.AttachThreadInput(self, foregroundThread, true);
            }

            _ = PInvoke.SetForegroundWindow(window);
            _ = PInvoke.BringWindowToTop(window);
        }
        finally
        {
            if (attached)
            {
                _ = PInvoke.AttachThreadInput(self, foregroundThread, false);
            }
        }
    }
}
