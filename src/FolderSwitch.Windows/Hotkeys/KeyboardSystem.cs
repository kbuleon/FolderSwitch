using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace FolderSwitch.Windows.Hotkeys;

/// <summary>The keyboard state and injection the hotkey logic needs from the system (a seam for tests).</summary>
internal interface IKeyboardSystem
{
    /// <summary>
    /// Injects a press and a release of the mask key. Sent while Win (or Alt) is still down, it makes
    /// Windows see a combination instead of the key alone, so its release does not open the Start menu
    /// (or the menu bar).
    /// </summary>
    void SendMaskKey();

    /// <summary>Whether the system reports the key as currently down (asynchronous key state).</summary>
    bool IsKeyDown(int virtualKey);
}

/// <summary>The real keyboard: <c>SendInput</c> and <c>GetAsyncKeyState</c>.</summary>
internal sealed class Win32KeyboardSystem : IKeyboardSystem
{
    /// <summary>VK 0xE8 is unassigned: applications ignore it.</summary>
    public const int MaskKey = 0xE8;

    public static Win32KeyboardSystem Instance { get; } = new();

    private Win32KeyboardSystem()
    {
    }

    public unsafe void SendMaskKey()
    {
        Span<INPUT> inputs = stackalloc INPUT[2];
        inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].ki.wVk = (VIRTUAL_KEY)MaskKey;
        inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].ki.wVk = (VIRTUAL_KEY)MaskKey;
        inputs[1].ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Nothing to do on failure (input blocked by UIPI towards an elevated window, say).
        _ = PInvoke.SendInput(inputs, sizeof(INPUT));
    }

    public bool IsKeyDown(int virtualKey) => (PInvoke.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
