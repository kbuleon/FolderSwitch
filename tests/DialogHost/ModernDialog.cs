// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;

namespace DialogHost;

/// <summary>
/// The Common Item Dialog (Vista and later) through COM, so that <c>IFileDialogEvents.OnFolderChange</c>
/// can report every folder change (the WPF wrappers do not expose it).
/// </summary>
internal static class ModernDialog
{
    private static readonly Guid FileOpenDialogClsid = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
    private static readonly Guid FileSaveDialogClsid = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");

    private const uint FOS_NOCHANGEDIR = 0x00000008;
    private const uint FOS_PICKFOLDERS = 0x00000020;

    public static void Show(DialogRequest request, nint owner)
    {
        var clsid = request.Mode == DialogMode.Save ? FileSaveDialogClsid : FileOpenDialogClsid;
        var dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, throwOnError: true)!)!;
        object? folder = null;
        uint cookie = 0;
        try
        {
            dialog.GetOptions(out var options);
            options |= FOS_NOCHANGEDIR;
            if (request.Mode == DialogMode.Folder)
            {
                options |= FOS_PICKFOLDERS;
            }

            dialog.SetOptions(options);
            Native.SHCreateItemFromParsingName(request.InitialFolder, 0, typeof(IShellItem).GUID, out folder);
            dialog.SetFolder((IShellItem)folder);
            if (request.FileName is { } fileName)
            {
                dialog.SetFileName(fileName);
            }

            dialog.Advise(new FolderChangeReporter(), out cookie);

            // Cancelled (also by WM_CLOSE on quit) or confirmed: either way the dialog has closed.
            _ = dialog.Show(owner);
        }
        finally
        {
            if (cookie != 0)
            {
                dialog.Unadvise(cookie);
            }

            if (folder is not null)
            {
                Marshal.ReleaseComObject(folder);
            }

            Marshal.ReleaseComObject(dialog);
        }
    }

    private sealed class FolderChangeReporter : IFileDialogEvents
    {
        private const int S_OK = 0;
        private const int E_NOTIMPL = unchecked((int)0x80004001);

        public int OnFileOk(IFileDialog pfd) => S_OK;

        public int OnFolderChanging(IFileDialog pfd, IShellItem psiFolder) => S_OK;

        public int OnFolderChange(IFileDialog pfd)
        {
            try
            {
                if (pfd.GetFolder(out var item) >= 0 && item is not null)
                {
                    try
                    {
                        if (item.GetDisplayName(Native.SIGDN_DESKTOPABSOLUTEPARSING, out var name) >= 0)
                        {
                            try
                            {
                                HostOutput.Folder(Marshal.PtrToStringUni(name));
                            }
                            finally
                            {
                                Marshal.FreeCoTaskMem(name);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(item);
                    }
                }
            }
            catch (Exception ex)
            {
                HostOutput.Line("ERROR OnFolderChange: " + ex.Message.ReplaceLineEndings(" "));
            }

            return S_OK;
        }

        public int OnSelectionChange(IFileDialog pfd) => S_OK;

        public int OnShareViolation(IFileDialog pfd, IShellItem psi, out int pResponse)
        {
            pResponse = 0;
            return E_NOTIMPL;
        }

        public int OnTypeChange(IFileDialog pfd) => S_OK;

        public int OnOverwrite(IFileDialog pfd, IShellItem psi, out int pResponse)
        {
            pResponse = 0;
            return E_NOTIMPL;
        }
    }
}

/// <summary><c>IFileDialog</c>, with the <c>IModalWindow.Show</c> method it inherits first.</summary>
[ComImport]
[Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileDialog
{
    [PreserveSig]
    int Show(nint parent);

    void SetFileTypes(uint count, nint filterSpec);

    void SetFileTypeIndex(uint index);

    void GetFileTypeIndex(out uint index);

    void Advise(IFileDialogEvents events, out uint cookie);

    void Unadvise(uint cookie);

    void SetOptions(uint options);

    void GetOptions(out uint options);

    void SetDefaultFolder(IShellItem folder);

    void SetFolder(IShellItem folder);

    [PreserveSig]
    int GetFolder(out IShellItem? folder);

    [PreserveSig]
    int GetCurrentSelection(out IShellItem? item);

    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

    void GetFileName(out nint name);

    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

    [PreserveSig]
    int GetResult(out IShellItem? item);

    void AddPlace(IShellItem item, int placement);

    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);

    void Close(int hr);

    void SetClientGuid(in Guid guid);

    void ClearClientData();

    void SetFilter(nint filter);
}

[ComImport]
[Guid("973510DB-7D7F-452B-8975-74A85828D354")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileDialogEvents
{
    [PreserveSig]
    int OnFileOk(IFileDialog pfd);

    [PreserveSig]
    int OnFolderChanging(IFileDialog pfd, IShellItem psiFolder);

    [PreserveSig]
    int OnFolderChange(IFileDialog pfd);

    [PreserveSig]
    int OnSelectionChange(IFileDialog pfd);

    [PreserveSig]
    int OnShareViolation(IFileDialog pfd, IShellItem psi, out int pResponse);

    [PreserveSig]
    int OnTypeChange(IFileDialog pfd);

    [PreserveSig]
    int OnOverwrite(IFileDialog pfd, IShellItem psi, out int pResponse);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(nint bindContext, in Guid handler, in Guid riid, out nint ppv);

    void GetParent(out IShellItem parent);

    [PreserveSig]
    int GetDisplayName(uint sigdn, out nint name);

    void GetAttributes(uint mask, out uint attributes);

    void Compare(IShellItem other, uint hint, out int order);
}
