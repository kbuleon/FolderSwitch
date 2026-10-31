using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.Dialogs;

/// <summary>Messages to windows of other processes, never waiting longer than a timeout.</summary>
internal static class WindowMessages
{
    /// <summary>
    /// <c>SendMessageTimeout</c> (<c>SMTO_ABORTIFHUNG | SMTO_BLOCK</c>). The message result, or
    /// <c>null</c> if the message failed or did not complete within <paramref name="timeoutMs"/>; the
    /// window may then still handle it later (see <see cref="RemoteMemory"/>).
    /// </summary>
    public static nuint? Send(nint window, uint message, nint wParam, nint lParam, uint timeoutMs)
    {
        var sent = PInvoke.SendMessageTimeout(
            (HWND)window,
            message,
            (WPARAM)(nuint)wParam,
            (LPARAM)lParam,
            SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK,
            timeoutMs,
            out var result);
        return sent.Value == 0 ? null : result;
    }

    /// <summary>
    /// The text of <paramref name="window"/> (<c>WM_GETTEXT</c>, whose buffer the system copies across
    /// processes): an empty string for none, <c>null</c> if the message did not complete in time or if
    /// the text fills the <paramref name="maxChars"/> buffer (it may have been cut).
    /// </summary>
    /// <remarks>
    /// The buffer is unmanaged and left allocated if the message does not complete: a window of this
    /// very process gets the pointer itself, and would write to it later.
    /// </remarks>
    public static string? GetText(nint window, int maxChars, uint timeoutMs)
    {
        var buffer = Marshal.AllocHGlobal(maxChars * sizeof(char));
        var length = Send(window, PInvoke.WM_GETTEXT, maxChars, buffer, timeoutMs);
        if (length is null)
        {
            return null;
        }

        try
        {
            return length.Value < (nuint)(maxChars - 1) ? Marshal.PtrToStringUni(buffer, (int)length.Value) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Sets the text of <paramref name="window"/> (<c>WM_SETTEXT</c>, whose string the system copies
    /// across processes). <c>false</c> if the window refused it, or if the message did not complete in
    /// time: the window may then still handle it later, after the messages sent to it before and
    /// before those sent after (sent messages are handled in order).
    /// </summary>
    /// <remarks>As for <see cref="GetText"/>, the string is left allocated if the message does not complete.</remarks>
    public static bool SetText(nint window, string text, uint timeoutMs)
    {
        var buffer = Marshal.StringToHGlobalUni(text);
        var result = Send(window, PInvoke.WM_SETTEXT, 0, buffer, timeoutMs);
        if (result is null)
        {
            return false;
        }

        Marshal.FreeHGlobal(buffer);
        return result.Value != 0;
    }
}
