using System.Runtime.InteropServices;
using System.Text;

namespace DialogHost;

/// <summary>The Win32 functions DialogHost uses (x64 and x86 layouts alike: only pointer-sized fields).</summary>
internal static class Native
{
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_NOTIFY = 0x004E;
    public const uint WM_USER = 0x0400;
    public const uint GW_OWNER = 4;

    /// <summary><c>SIGDN_DESKTOPABSOLUTEPARSING</c>: the parsing name, <c>::{CLSID}</c> for a virtual folder.</summary>
    public const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;

    public delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(nint hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    public static extern nint GetWindow(nint hwnd, uint command);

    [DllImport("user32.dll")]
    public static extern nint GetParent(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint SendMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHGetNameFromIDList(nint pidl, uint sigdn, out nint name);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object item);

    public static string ClassName(nint hwnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    /// <summary>The parsing name of an absolute ID list, or <c>null</c>.</summary>
    public static string? ParsingName(nint pidl)
    {
        if (pidl == 0 || SHGetNameFromIDList(pidl, SIGDN_DESKTOPABSOLUTEPARSING, out var name) < 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(name);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }
}
