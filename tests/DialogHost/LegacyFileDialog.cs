// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;

namespace DialogHost;

/// <summary>
/// The comdlg32 <c>GetOpenFileName</c> / <c>GetSaveFileName</c> dialogs, Explorer style. The hook
/// (<c>OFN_ENABLEHOOK</c>) keeps the pre-Vista look, as for the older applications that use one;
/// without it Windows would show the Common Item Dialog instead. The hook reports each
/// <c>CDN_FOLDERCHANGE</c>.
/// </summary>
internal static class LegacyFileDialog
{
    private const uint OFN_NOCHANGEDIR = 0x00000008;
    private const uint OFN_ENABLEHOOK = 0x00000020;
    private const uint OFN_EXPLORER = 0x00080000;
    private const uint OFN_ENABLESIZING = 0x00800000;

    private const int CDN_INITDONE = -601; // CDN_FIRST
    private const int CDN_FOLDERCHANGE = -603; // CDN_FIRST - 2
    private const uint CDM_GETFOLDERPATH = Native.WM_USER + 102;
    private const uint CDM_GETFOLDERIDLIST = Native.WM_USER + 103;
    private const uint CDM_SETCONTROLTEXT = Native.WM_USER + 104;
    private const uint WM_COMMAND = 0x0111;
    private const int FileNameControl = 0x47C; // cmb13
    private const int IDOK = 1;

    private const int FileBufferLength = 1024;

    // Kept alive while the dialog runs: native code holds a pointer to it.
    private static readonly HookProc Hook = HookProcedure;

    // lpstrInitialDir only takes a disk path: a virtual folder (::{CLSID}, shell:) is reached once the
    // dialog is up, by typing it in the file name box and pressing OK, as a user would.
    private static string? s_virtualInitialFolder;

    public static void Show(DialogRequest request, nint owner)
    {
        var fileBuffer = Marshal.AllocHGlobal(FileBufferLength * sizeof(char));
        var filter = Marshal.StringToHGlobalUni("All files (*.*)\0*.*\0\0");
        var isVirtual = request.InitialFolder.StartsWith("::", StringComparison.Ordinal)
            || request.InitialFolder.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
        s_virtualInitialFolder = isVirtual ? request.InitialFolder : null;
        var initialDir = isVirtual ? 0 : Marshal.StringToHGlobalUni(request.InitialFolder);
        try
        {
            var name = (request.FileName ?? "") + "\0";
            Marshal.Copy(name.ToCharArray(), 0, fileBuffer, Math.Min(name.Length, FileBufferLength - 1));
            Marshal.WriteInt16(fileBuffer, (FileBufferLength - 1) * sizeof(char), 0);

            var ofn = new OPENFILENAMEW
            {
                lStructSize = Marshal.SizeOf<OPENFILENAMEW>(),
                hwndOwner = owner,
                lpstrFilter = filter,
                nFilterIndex = 1,
                lpstrFile = fileBuffer,
                nMaxFile = FileBufferLength,
                lpstrInitialDir = initialDir,
                Flags = OFN_EXPLORER | OFN_ENABLEHOOK | OFN_ENABLESIZING | OFN_NOCHANGEDIR,
                lpfnHook = Marshal.GetFunctionPointerForDelegate(Hook),
            };

            // Cancelled (also by WM_CLOSE on quit) or confirmed: either way the dialog has closed.
            _ = request.Mode == DialogMode.LegacySave ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn);
            var error = CommDlgExtendedError();
            if (error != 0)
            {
                throw new InvalidOperationException("comdlg32 error 0x" + error.ToString("X"));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filter);
            Marshal.FreeHGlobal(initialDir);
        }
    }

    private static nint HookProcedure(nint hookDialog, uint msg, nint wParam, nint lParam)
    {
        // OFNOTIFYW starts with an NMHDR { HWND hwndFrom; UINT_PTR idFrom; UINT code; }.
        if (msg != Native.WM_NOTIFY)
        {
            return 0;
        }

        var dialog = Native.GetParent(hookDialog);
        switch (Marshal.ReadInt32(lParam, 2 * IntPtr.Size))
        {
            case CDN_INITDONE when s_virtualInitialFolder is { } folder:
                var text = Marshal.StringToHGlobalUni(folder);
                try
                {
                    Native.SendMessage(dialog, CDM_SETCONTROLTEXT, FileNameControl, text);
                }
                finally
                {
                    Marshal.FreeHGlobal(text);
                }

                Native.PostMessage(dialog, WM_COMMAND, IDOK, 0);
                break;
            case CDN_FOLDERCHANGE:
                HostOutput.Folder(CurrentFolder(dialog));
                break;
        }

        return 0;
    }

    private static string? CurrentFolder(nint dialog)
    {
        var buffer = Marshal.AllocHGlobal(FileBufferLength * sizeof(char));
        try
        {
            if (Native.SendMessage(dialog, CDM_GETFOLDERPATH, FileBufferLength, buffer) > 0)
            {
                return Marshal.PtrToStringUni(buffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        // A virtual folder has no path: its ID list gives the parsing name.
        var size = (int)Native.SendMessage(dialog, CDM_GETFOLDERIDLIST, 0, 0);
        if (size <= 0)
        {
            return null;
        }

        var pidl = Marshal.AllocCoTaskMem(size);
        try
        {
            return Native.SendMessage(dialog, CDM_GETFOLDERIDLIST, size, pidl) > 0 ? Native.ParsingName(pidl) : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    private delegate nint HookProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct OPENFILENAMEW
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public nint lpstrFilter;
        public nint lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        public nint lpstrInitialDir;
        public nint lpstrTitle;
        public uint Flags;
        public ushort nFileOffset;
        public ushort nFileExtension;
        public nint lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OPENFILENAMEW ofn);

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(ref OPENFILENAMEW ofn);

    [DllImport("comdlg32.dll")]
    private static extern uint CommDlgExtendedError();
}
