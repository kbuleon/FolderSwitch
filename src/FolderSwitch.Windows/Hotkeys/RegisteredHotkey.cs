using FolderSwitch.Core.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace FolderSwitch.Windows.Hotkeys;

/// <summary>
/// A combination with a regular key, registered with <c>RegisterHotKey</c> on a window (which then
/// receives <c>WM_HOTKEY</c> with <see cref="Id"/>); disposing it unregisters it. Use it from the
/// window's thread.
/// </summary>
internal sealed class RegisteredHotkey : IDisposable
{
    private readonly HWND _window;
    private bool _disposed;

    private RegisteredHotkey(HWND window, int id, Hotkey hotkey)
    {
        _window = window;
        Id = id;
        Hotkey = hotkey;
    }

    public int Id { get; }

    public Hotkey Hotkey { get; }

    /// <summary>Registers <paramref name="hotkey"/>, auto-repeat excluded.</summary>
    /// <returns><c>null</c> if the combination is taken (by another application or another registration).</returns>
    public static RegisteredHotkey? TryRegister(HWND window, int id, Hotkey hotkey) =>
        Register(window, id, hotkey) ? new RegisteredHotkey(window, id, hotkey) : null;

    /// <summary>Registers then immediately releases <paramref name="hotkey"/>.</summary>
    /// <returns><c>false</c> if the combination is taken.</returns>
    public static bool IsFree(HWND window, int id, Hotkey hotkey)
    {
        if (!Register(window, id, hotkey))
        {
            return false;
        }

        PInvoke.UnregisterHotKey(window, id);
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            PInvoke.UnregisterHotKey(_window, Id);
        }
    }

    internal static HOT_KEY_MODIFIERS ToNative(KeyModifiers modifiers)
    {
        var native = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) native |= HOT_KEY_MODIFIERS.MOD_CONTROL;
        if (modifiers.HasFlag(KeyModifiers.Alt)) native |= HOT_KEY_MODIFIERS.MOD_ALT;
        if (modifiers.HasFlag(KeyModifiers.Shift)) native |= HOT_KEY_MODIFIERS.MOD_SHIFT;
        if (modifiers.HasFlag(KeyModifiers.Win)) native |= HOT_KEY_MODIFIERS.MOD_WIN;
        return native;
    }

    private static bool Register(HWND window, int id, Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        if (hotkey.VirtualKey is not { } vk)
        {
            throw new ArgumentException("A modifier-only hotkey cannot be registered with RegisterHotKey.", nameof(hotkey));
        }

        return PInvoke.RegisterHotKey(window, id, ToNative(hotkey.Modifiers), (uint)vk);
    }
}
