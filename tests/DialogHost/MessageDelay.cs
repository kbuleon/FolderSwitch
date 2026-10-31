using System.Runtime.InteropServices;

namespace DialogHost;

/// <summary>
/// <c>slow &lt;ms&gt;</c>: the next message that reads the current folder through a pointer, from any
/// process, is held for that long <b>after</b> the dialog has taken it and before the dialog handles
/// it: <c>TVM_GETITEMW</c> on a folder picker's tree, <c>CDM_GETFOLDERIDLIST</c> with a buffer on an
/// Open / Save As dialog. A sender with a shorter timeout gives up while the dialog still writes
/// through the pointer afterwards. The window is subclassed (on the UI thread) the first time.
/// Reported by <c>SLOW ARMED</c>, then <c>SLOW</c> when the message is held and <c>SLOW DONE</c> once
/// the dialog has handled it.
/// </summary>
internal static class MessageDelay
{
    private const int GWLP_WNDPROC = -4;
    private const uint TVM_GETITEMW = 0x1100 + 62;
    private const uint CDM_GETFOLDERIDLIST = Native.WM_USER + 103;

    // Kept alive while the windows live: native code holds a pointer to it.
    private static readonly WndProc Procedure = SubclassProcedure;
    private static readonly Dictionary<nint, (nint Original, uint Message)> Subclassed = [];
    private static int s_delayMs;

    /// <summary>Arms the delay for <paramref name="dialog"/>. Call on the dialog's thread.</summary>
    public static void Arm(nint dialog, int milliseconds)
    {
        var tree = FolderPickerTree(dialog);
        var (window, message) = tree != 0 ? (tree, TVM_GETITEMW) : (dialog, CDM_GETFOLDERIDLIST);
        if (!Subclassed.ContainsKey(window))
        {
            var original = SetWindowLongPtr(window, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(Procedure));
            Subclassed[window] = (original, message);
        }

        Interlocked.Exchange(ref s_delayMs, milliseconds);
        HostOutput.Line("SLOW ARMED");
    }

    private static nint SubclassProcedure(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        var (original, message) = Subclassed[hwnd];
        if (msg == message && lParam != 0 && Interlocked.Exchange(ref s_delayMs, 0) is var delay && delay > 0)
        {
            HostOutput.Line("SLOW");
            Thread.Sleep(delay);
            var result = CallWindowProc(original, hwnd, msg, wParam, lParam);
            HostOutput.Line("SLOW DONE");
            return result;
        }

        return CallWindowProc(original, hwnd, msg, wParam, lParam);
    }

    /// <summary>The tree of a legacy folder picker (hosted, or the classic direct child), or zero.</summary>
    private static nint FolderPickerTree(nint dialog)
    {
        var host = FindWindowEx(dialog, 0, "SHBrowseForFolder ShellNameSpace Control", null);
        if (host != 0)
        {
            return FindWindowEx(host, 0, "SysTreeView32", null);
        }

        var tree = FindWindowEx(dialog, 0, "SysTreeView32", null);
        return tree != 0 && GetDlgCtrlID(tree) == 0x3741 ? tree : 0;
    }

    private delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProc(nint previous, nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
    private static extern nint FindWindowEx(nint parent, nint after, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern int GetDlgCtrlID(nint hwnd);
}
