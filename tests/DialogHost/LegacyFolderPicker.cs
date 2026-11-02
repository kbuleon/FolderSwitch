// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;

namespace DialogHost;

/// <summary>
/// The legacy folder picker, <c>SHBrowseForFolder</c>: the initial folder is selected with
/// <c>BFFM_SETSELECTIONW</c> on <c>BFFM_INITIALIZED</c>, and each <c>BFFM_SELCHANGED</c> is reported.
/// </summary>
internal static class LegacyFolderPicker
{
    private const uint BIF_RETURNONLYFSDIRS = 0x00000001;
    private const uint BIF_NEWDIALOGSTYLE = 0x00000040;

    private const uint BFFM_INITIALIZED = 1;
    private const uint BFFM_SELCHANGED = 2;
    private const uint BFFM_SETSELECTIONW = Native.WM_USER + 103;

    private static readonly BrowseCallbackProc Callback = CallbackProcedure;

    public static void Show(DialogRequest request, nint owner)
    {
        var displayName = Marshal.AllocHGlobal(260 * sizeof(char));
        var initialFolder = Marshal.StringToHGlobalUni(request.InitialFolder);
        try
        {
            var info = new BROWSEINFOW
            {
                hwndOwner = owner,
                pszDisplayName = displayName,
                ulFlags = BIF_RETURNONLYFSDIRS | (request.Mode == DialogMode.LegacyFolder ? BIF_NEWDIALOGSTYLE : 0),
                lpfn = Marshal.GetFunctionPointerForDelegate(Callback),
                lParam = initialFolder,
            };

            // Cancelled (also by WM_CLOSE on quit) or confirmed: either way the dialog has closed.
            var result = SHBrowseForFolder(ref info);
            if (result != 0)
            {
                Marshal.FreeCoTaskMem(result);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(displayName);
            Marshal.FreeHGlobal(initialFolder);
        }
    }

    private static int CallbackProcedure(nint hwnd, uint msg, nint lParam, nint data)
    {
        switch (msg)
        {
            case BFFM_INITIALIZED:
                Native.SendMessage(hwnd, BFFM_SETSELECTIONW, 1, data);
                break;
            case BFFM_SELCHANGED:
                HostOutput.Folder(Native.ParsingName(lParam));
                break;
        }

        return 0;
    }

    private delegate int BrowseCallbackProc(nint hwnd, uint msg, nint lParam, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct BROWSEINFOW
    {
        public nint hwndOwner;
        public nint pidlRoot;
        public nint pszDisplayName;
        public nint lpszTitle;
        public uint ulFlags;
        public nint lpfn;
        public nint lParam;
        public int iImage;
    }

    [DllImport("shell32.dll", EntryPoint = "SHBrowseForFolderW")]
    private static extern nint SHBrowseForFolder(ref BROWSEINFOW info);
}
