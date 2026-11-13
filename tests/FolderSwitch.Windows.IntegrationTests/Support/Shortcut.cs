// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>Writes shell shortcuts (<c>.lnk</c>) the way the shell does: <c>CLSID_ShellLink</c>, saved through <c>IPersistFile</c>.</summary>
internal static class Shortcut
{
    /// <summary>Writes the shortcut <paramref name="linkPath"/> to <paramref name="target"/>, a file or a folder.</summary>
    public static void Create(string linkPath, string target) => Sta.Run(() =>
    {
        var link = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellGuids.ShellLink, throwOnError: true)!)!;
        try
        {
            ((IShellLinkWriter)link).SetPath(target);
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(linkPath, fRemember: true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    });
}

/// <summary><c>IShellLinkW</c>, up to <c>SetPath</c>, to write a shortcut.</summary>
[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkWriter
{
    void GetPath(nint file, int size, nint findData, uint flags);

    void GetIDList(out nint pidl);

    void SetIDList(nint pidl);

    void GetDescription(nint name, int size);

    void SetDescription(nint name);

    void GetWorkingDirectory(nint directory, int size);

    void SetWorkingDirectory(nint directory);

    void GetArguments(nint arguments, int size);

    void SetArguments(nint arguments);

    void GetHotkey(out ushort hotkey);

    void SetHotkey(ushort hotkey);

    void GetShowCmd(out int showCommand);

    void SetShowCmd(int showCommand);

    void GetIconLocation(nint iconPath, int size, out int index);

    void SetIconLocation(nint iconPath, int index);

    void SetRelativePath(nint relativePath, uint reserved);

    void Resolve(nint hwnd, uint flags);

    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}
