using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.Hotkeys;

/// <summary>
/// A <c>WH_KEYBOARD_LL</c> hook. Install it from a thread that runs a message loop: Windows calls the
/// handler on that thread, while it waits for messages, and holds the keyboard input until it returns,
/// so the handler must be fast and never block. Injected events (<c>LLKHF_INJECTED</c>, such as the
/// mask key FolderSwitch sends) are passed on without reaching the handler.
/// </summary>
internal sealed class LowLevelKeyboardHook : IDisposable
{
    private readonly KeyHandler _onKey;
    private readonly Action<Exception> _onError;

    // Referenced for as long as the hook may call it: the native thunk lives as long as the delegate.
    private readonly HOOKPROC _callback;

    private HHOOK _hook;

    /// <summary>Handles a physical key event; returns <c>true</c> to swallow it.</summary>
    public delegate bool KeyHandler(int virtualKey, bool isKeyDown);

    /// <param name="onKey">Called for every physical key press (auto-repeats included) and release.</param>
    /// <param name="onError">
    /// Receives an exception thrown by <paramref name="onKey"/> (it must not unwind through the native
    /// caller); the event is then passed on.
    /// </param>
    public LowLevelKeyboardHook(KeyHandler onKey, Action<Exception> onError)
    {
        _onKey = onKey;
        _onError = onError;
        _callback = Callback;
    }

    public bool IsInstalled => !_hook.IsNull;

    /// <summary>How many hooks were installed so far, reinstallations included (for tests).</summary>
    public int InstallCount { get; private set; }

    /// <exception cref="Win32Exception">The hook could not be installed.</exception>
    public void Install()
    {
        if (!IsInstalled)
        {
            _hook = SetHook();
        }
    }

    /// <summary>
    /// Replaces the installed hook with a fresh one. Windows silently removes a low-level hook whose
    /// handler timed out; reinstalling from time to time is a cheap safety net. If a new hook cannot be
    /// installed, the current one is kept. Call it from the hook's thread, outside the handler: no event
    /// reaches either hook until that thread waits for messages again, so none is lost or seen twice.
    /// </summary>
    public void Reinstall()
    {
        if (!IsInstalled)
        {
            return;
        }

        HHOOK fresh;
        try
        {
            fresh = SetHook();
        }
        catch (Win32Exception)
        {
            return;
        }

        // Fails harmlessly if Windows already removed the old hook.
        PInvoke.UnhookWindowsHookEx(_hook);
        _hook = fresh;
    }

    public void Uninstall()
    {
        if (IsInstalled)
        {
            PInvoke.UnhookWindowsHookEx(_hook);
            _hook = default;
        }
    }

    public void Dispose() => Uninstall();

    private HHOOK SetHook()
    {
        // A low-level hook runs in the installing process; any module handle of the process will do.
        var module = PInvoke.GetModuleHandle(default(PCWSTR));
        var hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _callback, module, 0);
        if (hook.IsNull)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        InstallCount++;
        return hook;
    }

    private unsafe LRESULT Callback(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam.Value;
            if ((info->flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED) == 0 && IsKeyDown((uint)wParam.Value) is { } isKeyDown)
            {
                try
                {
                    if (_onKey((int)info->vkCode, isKeyDown))
                    {
                        return (LRESULT)1;
                    }
                }
                catch (Exception e)
                {
                    _onError(e);
                }
            }
        }

        return PInvoke.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool? IsKeyDown(uint message) => message switch
    {
        PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN => true,
        PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP => false,
        _ => null,
    };
}
