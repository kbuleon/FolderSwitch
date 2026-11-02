// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;

namespace DialogHost;

/// <summary>
/// <c>drop</c> / <c>drop reset</c>: the next validation the dialog takes, from any process, is dropped
/// unhandled: <c>WM_COMMAND IDOK</c> (a click on OK) for an Open / Save As dialog or the Common Item
/// Dialog's folder picker, <c>BFFM_SETSELECTIONW</c> for a legacy folder picker. With <c>reset</c>, the
/// file name box text is replaced by <c>dropped</c> meanwhile, as a dialog that is done with an OK
/// puts a text of its own back; without, it keeps what it holds, as while a slow move is under way.
/// <para>
/// <c>slow-ok &lt;ms&gt;</c>: the next OK (<c>WM_COMMAND IDOK</c>), once taken, holds the dialog's
/// thread for that long, then is handled through a message the dialog posts to itself: after every
/// message sent to it meanwhile, as a dialog that defers its OK does (sent messages come before posted
/// ones). A sender with a shorter timeout gives up first. Reported by <c>SLOW OK ARMED</c>, then
/// <c>SLOW OK</c> when it is held, and <c>PASSED</c> when the reposted OK is handled.
/// </para>
/// The dialog window is subclassed (on the UI thread) the first time. Reported by <c>DROP ARMED</c>,
/// then <c>DROPPED</c> for the dropped one, and <c>PASSED</c> for each later one, which the dialog
/// handles.
/// </summary>
internal static class ValidationDrop
{
    private const int GWLP_WNDPROC = -4;
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_SETTEXT = 0x000C;
    private const uint BFFM_SETSELECTIONW = Native.WM_USER + 103; // CDM_GETFOLDERIDLIST for Open / Save As
    private const int IDOK = 1;
    private const int FileNameCombo = 0x47C;
    private const int FileNameEdit = 0x480;

    // Kept alive while the window lives: native code holds a pointer to it.
    private static readonly WndProc Procedure = SubclassProcedure;
    private static nint s_dialog;
    private static nint s_original;
    private static bool s_isFolderPicker;
    private static bool s_armed;
    private static bool s_reset;
    private static int s_slowOkMs;

    /// <summary>Arms the drop for <paramref name="dialog"/>. Call on the dialog's thread.</summary>
    public static void Arm(nint dialog, bool reset)
    {
        Subclass(dialog);
        s_reset = reset;
        s_armed = true;
        HostOutput.Line("DROP ARMED");
    }

    /// <summary>Arms <c>slow-ok</c> for <paramref name="dialog"/>. Call on the dialog's thread.</summary>
    public static void ArmSlowOk(nint dialog, int milliseconds)
    {
        Subclass(dialog);
        s_slowOkMs = milliseconds;
        HostOutput.Line("SLOW OK ARMED");
    }

    private static void Subclass(nint dialog)
    {
        if (s_dialog != dialog)
        {
            s_isFolderPicker = FindWindowEx(dialog, 0, "SHBrowseForFolder ShellNameSpace Control", null) != 0
                || (FindWindowEx(dialog, 0, "SysTreeView32", null) is var tree && tree != 0 && GetDlgCtrlID(tree) == 0x3741);
            s_original = SetWindowLongPtr(dialog, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(Procedure));
            s_dialog = dialog;
        }
    }

    private static nint SubclassProcedure(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        var isOk = msg == WM_COMMAND && (wParam & 0xFFFF) == IDOK;
        var validation = s_isFolderPicker ? msg == BFFM_SETSELECTIONW : isOk;
        if (validation)
        {
            if (s_armed)
            {
                s_armed = false;
                if (s_reset && FileNameBox(hwnd) is var box && box != 0)
                {
                    SendMessage(box, WM_SETTEXT, 0, "dropped");
                }

                HostOutput.Line("DROPPED");
                return 0;
            }

            if (isOk && s_slowOkMs > 0)
            {
                var delay = s_slowOkMs;
                s_slowOkMs = 0;
                HostOutput.Line("SLOW OK");
                Thread.Sleep(delay);
                Native.PostMessage(hwnd, msg, wParam, lParam);
                return 0;
            }

            HostOutput.Line("PASSED");
        }

        return CallWindowProc(s_original, hwnd, msg, wParam, lParam);
    }

    /// <summary>The edit of the file name box (the places measured on Windows 11), or zero.</summary>
    private static nint FileNameBox(nint dialog)
    {
        var combo = GetDlgItem(dialog, FileNameCombo);
        if (combo != 0)
        {
            return FindWindowEx(FindWindowEx(combo, 0, "ComboBox", null), 0, "Edit", null);
        }

        var edit = GetDlgItem(dialog, FileNameEdit);
        if (edit != 0)
        {
            return edit;
        }

        // Save As (Common Item Dialog): the first FloatNotifySink of DirectUI holds the file name combo box.
        var directUi = FindWindowEx(FindWindowEx(dialog, 0, "DUIViewWndClassName", null), 0, "DirectUIHWND", null);
        var sink = FindWindowEx(directUi, 0, "FloatNotifySink", null);
        return FindWindowEx(FindWindowEx(sink, 0, "ComboBox", null), 0, "Edit", null);
    }

    private delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProc(nint previous, nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
    private static extern nint FindWindowEx(nint parent, nint after, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern nint GetDlgItem(nint dialog, int id);

    [DllImport("user32.dll")]
    private static extern int GetDlgCtrlID(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hwnd, uint msg, nint wParam, string lParam);
}
