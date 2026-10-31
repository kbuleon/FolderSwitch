using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Hotkeys;

/// <summary>Win32 virtual-key codes used by the hotkey logic, plus their mapping to <see cref="KeyModifiers"/>.</summary>
public static class VirtualKeys
{
    public const int Tab = 0x09;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int Delete = 0x2E;

    // The generic modifier codes (what GetKeyState-style APIs report) and their left/right variants
    // (what a low-level keyboard hook reports).
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;

    // Letters (0x41-0x5A) and digits (0x30-0x39) share their code with the ASCII upper-case character.
    public const int Digit0 = 0x30;
    public const int Digit9 = 0x39;
    public const int A = 0x41;
    public const int D = 0x44;
    public const int E = 0x45;
    public const int L = 0x4C;
    public const int R = 0x52;
    public const int S = 0x53;
    public const int Z = 0x5A;

    public const int F1 = 0x70;
    public const int F4 = 0x73;
    public const int F24 = 0x87;

    /// <summary>
    /// The modifier a virtual key stands for, or <c>null</c> when it is not a modifier. Left, right
    /// and generic variants of a modifier map to the same flag.
    /// </summary>
    public static KeyModifiers? ModifierOf(int vk) => vk switch
    {
        LControl or RControl or Control => KeyModifiers.Ctrl,
        LMenu or RMenu or Menu => KeyModifiers.Alt,
        LShift or RShift or Shift => KeyModifiers.Shift,
        LWin or RWin => KeyModifiers.Win,
        _ => null,
    };
}
