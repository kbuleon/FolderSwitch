using System.Runtime.InteropServices;

namespace FolderSwitch.Windows.Shell;

// Shell COM interfaces, declared by hand as built-in COM interop ([ComImport]) rather than generated
// by CsWin32: CsWin32 only generates IShellBrowser for a fixed CPU architecture, and its projection
// throws on any failure HRESULT, where these lookups expect failures (closed window, virtual folder)
// and test HRESULTs instead ([PreserveSig]). Only the vtable slots up to the last method used are
// declared, in vtable order; unused slots take placeholder signatures.
//
// Early binding (no `dynamic`): the C# runtime binder costs far more than the 50 ms capture budget on
// first use, and IServiceProvider / IShellBrowser are not IDispatch interfaces anyway.

/// <summary>GUIDs of the shell objects and services used with these interfaces.</summary>
internal static class ShellGuids
{
    /// <summary>CLSID_ShellWindows: the collection of open Explorer windows (one entry per tab on Windows 11).</summary>
    public static readonly Guid ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");

    /// <summary>SID_STopLevelBrowser: the service giving the top-level <see cref="IShellBrowser"/> of a browser.</summary>
    public static readonly Guid TopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
}

/// <summary><c>IShellWindows</c> (dual): only <c>Count</c> and <c>Item</c>.</summary>
[ComImport]
[Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IShellWindows
{
    [PreserveSig]
    int get_Count(out int count);

    /// <returns>S_FALSE and <c>null</c> when <paramref name="index"/> is out of range.</returns>
    [PreserveSig]
    int Item([MarshalAs(UnmanagedType.Struct)] object index, [MarshalAs(UnmanagedType.IDispatch)] out object? folder);
}

/// <summary>
/// <c>IWebBrowser2</c> (dual), with the <c>IWebBrowser</c> and <c>IWebBrowserApp</c> methods it
/// inherits, up to <c>Navigate2</c>.
/// </summary>
[ComImport]
[Guid("D30C1661-CDAF-11D0-8A3E-00C04FC9E26E")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IWebBrowser2
{
    // ---- IWebBrowser ----
    void GoBack();

    void GoForward();

    void GoHome();

    void GoSearch();

    void Navigate(nint url, nint flags, nint targetFrameName, nint postData, nint headers);

    void Refresh();

    void Refresh2(nint level);

    void Stop();

    void get_Application(out nint application);

    void get_Parent(out nint parent);

    void get_Container(out nint container);

    void get_Document(out nint document);

    void get_TopLevelContainer(out short value);

    void get_Type(out nint type);

    void get_Left(out int value);

    void put_Left(int value);

    void get_Top(out int value);

    void put_Top(int value);

    void get_Width(out int value);

    void put_Width(int value);

    void get_Height(out int value);

    void put_Height(int value);

    void get_LocationName(out nint name);

    void get_LocationURL(out nint url);

    void get_Busy(out short value);

    // ---- IWebBrowserApp ----

    /// <summary>Closes the browser: the Explorer window, or only this tab on Windows 11.</summary>
    [PreserveSig]
    int Quit();

    void ClientToWindow(ref int cx, ref int cy);

    void PutProperty(nint property, nint value);

    void GetProperty(nint property, nint value);

    void get_Name(out nint name);

    /// <summary>The top-level window of the browser (the Explorer window, shared by its tabs).</summary>
    [PreserveSig]
    int get_HWND(out nint hwnd);

    void get_FullName(out nint name);

    void get_Path(out nint path);

    void get_Visible(out short value);

    void put_Visible(short value);

    void get_StatusBar(out short value);

    void put_StatusBar(short value);

    void get_StatusText(out nint text);

    void put_StatusText(nint text);

    void get_ToolBar(out int value);

    void put_ToolBar(int value);

    void get_MenuBar(out short value);

    void put_MenuBar(short value);

    void get_FullScreen(out short value);

    void put_FullScreen(short value);

    // ---- IWebBrowser2 ----

    /// <summary>Navigates to a location given as a path / parsing name string, or as a PIDL (VT_ARRAY | VT_UI1).</summary>
    [PreserveSig]
    int Navigate2(
        [MarshalAs(UnmanagedType.Struct)] in object url,
        [MarshalAs(UnmanagedType.Struct)] in object flags,
        [MarshalAs(UnmanagedType.Struct)] in object targetFrameName,
        [MarshalAs(UnmanagedType.Struct)] in object postData,
        [MarshalAs(UnmanagedType.Struct)] in object headers);
}

/// <summary>COM <c>IServiceProvider</c> (not <see cref="System.IServiceProvider"/>).</summary>
[ComImport]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IComServiceProvider
{
    [PreserveSig]
    int QueryService(in Guid service, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? result);
}

/// <summary><c>IShellBrowser</c> (with the <c>IOleWindow</c> methods it inherits), up to <c>QueryActiveShellView</c>.</summary>
[ComImport]
[Guid("000214E2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    /// <summary>The browser's window: its <c>ShellTabWindowClass</c> tab window on Windows 11.</summary>
    [PreserveSig]
    int GetWindow(out nint hwnd);

    void ContextSensitiveHelp(int enterMode);

    void InsertMenusSB(nint sharedMenu, nint menuWidths);

    void SetMenuSB(nint sharedMenu, nint oleMenu, nint activeObject);

    void RemoveMenusSB(nint sharedMenu);

    void SetStatusTextSB(nint text);

    void EnableModelessSB(int enable);

    void TranslateAcceleratorSB(nint msg, ushort id);

    void BrowseObject(nint pidl, uint flags);

    void GetViewStateStream(uint mode, out nint stream);

    void GetControlWindow(uint id, out nint hwnd);

    void SendControlMsg(uint id, uint msg, nint wParam, nint lParam, out nint result);

    /// <summary>The <c>IShellView</c> of the folder shown (queried for <see cref="IFolderView"/>).</summary>
    [PreserveSig]
    int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object? view);
}

/// <summary><c>IFolderView</c>, up to <c>GetFolder</c>.</summary>
[ComImport]
[Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView
{
    void GetCurrentViewMode(out uint mode);

    void SetCurrentViewMode(uint mode);

    [PreserveSig]
    int GetFolder(in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? folder);
}

/// <summary><c>IPersistFolder2</c>, with the <c>IPersist</c> and <c>IPersistFolder</c> methods it inherits.</summary>
[ComImport]
[Guid("1AC3D9F0-175C-11D1-95BE-00609797EA4F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistFolder2
{
    void GetClassID(out Guid clsid);

    void Initialize(nint pidl);

    /// <summary>The absolute ID list of the folder, allocated with <c>CoTaskMemAlloc</c>.</summary>
    [PreserveSig]
    int GetCurFolder(out nint pidl);
}

/// <summary><c>IShellFolder</c>, up to <c>GetDisplayNameOf</c>.</summary>
[ComImport]
[Guid("000214E6-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellFolder
{
    [PreserveSig]
    int ParseDisplayName(
        nint hwnd,
        nint bindContext,
        [MarshalAs(UnmanagedType.LPWStr)] string displayName,
        out uint eaten,
        out nint pidl,
        ref uint attributes);

    /// <returns>S_FALSE and a <c>null</c> enumerator when there is nothing to enumerate.</returns>
    [PreserveSig]
    int EnumObjects(nint hwnd, uint flags, out IEnumIDList? enumerator);

    [PreserveSig]
    int BindToObject(nint pidl, nint bindContext, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? result);

    void BindToStorage(nint pidl, nint bindContext, in Guid riid, out nint result);

    void CompareIDs(nint lParam, nint pidl1, nint pidl2);

    void CreateViewObject(nint owner, in Guid riid, out nint result);

    void GetAttributesOf(uint count, nint pidls, ref uint attributes);

    void GetUIObjectOf(nint owner, uint count, nint pidls, in Guid riid, nint reserved, out nint result);

    [PreserveSig]
    int GetDisplayNameOf(nint pidl, uint flags, out STRRET name);
}

[ComImport]
[Guid("000214F2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumIDList
{
    /// <returns>S_OK with one item, S_FALSE at the end.</returns>
    [PreserveSig]
    int Next(uint count, out nint pidl, out uint fetched);
}

/// <summary>The <c>STRRET</c> returned by <see cref="IShellFolder.GetDisplayNameOf"/>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct STRRET
{
    public uint uType;
    public STRRETUnion Data;
}

[StructLayout(LayoutKind.Explicit, Size = 260)]
internal struct STRRETUnion
{
    [FieldOffset(0)]
    public nint pOleStr;
}

/// <summary>
/// <c>SHELLEXECUTEINFOW</c>, declared by hand: CsWin32 only generates it for a fixed CPU architecture
/// (the header packs it to 1 byte on 32-bit Windows, where every field is 4 bytes wide anyway).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ShellExecuteInfo
{
    public int cbSize;
    public uint fMask;
    public nint hwnd;
    public string? lpVerb;
    public string? lpFile;
    public string? lpParameters;
    public string? lpDirectory;
    public int nShow;
    public nint hInstApp;
    public nint lpIDList;
    public string? lpClass;
    public nint hkeyClass;
    public uint dwHotKey;
    public nint hIconOrMonitor;
    public nint hProcess;
}

/// <summary>
/// <c>SHFILEINFOW</c>, declared by hand: CsWin32 only generates it for a fixed CPU architecture (the header packs it to
/// 1 byte on 32-bit Windows, where the natural layout is the same).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ShFileInfo
{
    public nint hIcon;
    public int iIcon;
    public uint dwAttributes;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szDisplayName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string szTypeName;
}

/// <summary>Flat shell functions working on ID lists (<c>nint</c> PIDLs, freed with <c>CoTaskMemFree</c>).</summary>
internal static class ShellNative
{
    public const int S_OK = 0;

    /// <summary><c>SIGDN_DESKTOPABSOLUTEPARSING</c>: a path, or <c>::{CLSID}</c> for a virtual location.</summary>
    public const uint SigdnDesktopAbsoluteParsing = 0x80028000;

    /// <summary><c>SIGDN_FILESYSPATH</c>: the file-system path; fails for a location that has none.</summary>
    public const uint SigdnFileSysPath = 0x80058000;

    /// <summary><c>SIGDN_NORMALDISPLAY</c>: the name the shell shows for the item (« Home », « Documents »...).</summary>
    public const uint SigdnNormalDisplay = 0x00000000;

    /// <summary><c>SFGAO_FOLDER</c>: the item contains items (a folder, a drive, This PC... and a .zip file).</summary>
    public const uint SfgaoFolder = 0x20000000;

    /// <summary><c>SFGAO_FILESYSTEM</c>: the item is part of the file system.</summary>
    public const uint SfgaoFileSystem = 0x40000000;

    /// <summary><c>SFGAO_FILESYSANCESTOR</c>: the item contains file-system folders (This PC, Libraries, Network...).</summary>
    public const uint SfgaoFileSysAncestor = 0x10000000;

    /// <summary><c>SFGAO_STREAM</c>: the item has a stream, i.e. it is a file (including a .zip "folder").</summary>
    public const uint SfgaoStream = 0x00400000;

    public const uint ShgdnNormal = 0x0000;
    public const uint ShgdnInFolder = 0x0001;

    public const uint ShcontfFolders = 0x0020;
    public const uint ShcontfIncludeHidden = 0x0080;

    [DllImport("shell32.dll", ExactSpelling = true)]
    public static extern int SHGetDesktopFolder([MarshalAs(UnmanagedType.Interface)] out IShellFolder? folder);

    [DllImport("shell32.dll", ExactSpelling = true)]
    public static extern int SHGetNameFromIDList(nint pidl, uint sigdn, out nint name);

    [DllImport("shell32.dll", ExactSpelling = true)]
    public static extern nint ILCombine(nint parent, nint child);

    [DllImport("shlwapi.dll", ExactSpelling = true)]
    public static extern int StrRetToStrW(ref STRRET strret, nint pidl, out nint text);

    /// <summary>Parses a path, <c>shell:</c> or <c>::{CLSID}</c> name into an absolute ID list (STA thread).</summary>
    [DllImport("shell32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern int SHParseDisplayName(string name, nint bindContext, out nint pidl, uint attributesIn, out uint attributesOut);

    /// <summary>The size in bytes of an ID list, its terminator included.</summary>
    [DllImport("shell32.dll", ExactSpelling = true)]
    public static extern uint ILGetSize(nint pidl);

    /// <summary><c>SEE_MASK_NOASYNC</c>: the launch is complete when <see cref="ShellExecuteEx"/> returns.</summary>
    public const uint SeeMaskNoAsync = 0x00000100;

    /// <summary><c>SEE_MASK_FLAG_NO_UI</c>: no error message box; the failure is only returned.</summary>
    public const uint SeeMaskFlagNoUi = 0x00000400;

    public const int SwShowNormal = 1;

    /// <summary><c>SHGFI_ICON</c>: the icon handle is returned in <see cref="ShFileInfo.hIcon"/>.</summary>
    public const uint ShgfiIcon = 0x00000100;

    /// <summary><c>SHGFI_SMALLICON</c>: the small icon, with <see cref="ShgfiIcon"/>.</summary>
    public const uint ShgfiSmallIcon = 0x00000001;

    /// <summary><c>SHGFI_USEFILEATTRIBUTES</c>: the name is only an extension to look up: the file is not touched.</summary>
    public const uint ShgfiUseFileAttributes = 0x00000010;

    /// <summary><c>FILE_ATTRIBUTE_NORMAL</c>.</summary>
    public const uint FileAttributeNormal = 0x00000080;

    /// <summary>File information; with <see cref="ShgfiIcon"/> the icon is to be released with <c>DestroyIcon</c>.</summary>
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern nuint SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo info, uint size, uint flags);

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteExW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShellExecuteEx(ref ShellExecuteInfo info);

    /// <summary>
    /// The absolute ID list of a parsing name (a path, <c>shell:...</c>, <c>::{CLSID}</c>) as bytes: the
    /// form <c>IWebBrowser2.Navigate2</c> takes an ID list in (a <c>VT_ARRAY | VT_UI1</c> variant).
    /// <c>null</c> if the name does not parse. STA thread.
    /// </summary>
    public static byte[]? IdListBytes(string parsingName)
    {
        var hr = SHParseDisplayName(parsingName, 0, out var pidl, 0, out _);
        if (pidl == 0)
        {
            return null;
        }

        try
        {
            if (hr != S_OK)
            {
                return null;
            }

            var bytes = new byte[ILGetSize(pidl)];
            Marshal.Copy(pidl, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    /// <summary>The parsing name of an absolute ID list, or <c>null</c>.</summary>
    public static string? ParsingName(nint pidl) => Name(pidl, SigdnDesktopAbsoluteParsing);

    /// <summary>The file-system path of an absolute ID list, or <c>null</c> for a location without one.</summary>
    public static string? FileSystemPath(nint pidl) => Name(pidl, SigdnFileSysPath);

    /// <summary>The name the shell shows for an absolute ID list, or <c>null</c>.</summary>
    public static string? DisplayName(nint pidl) => Name(pidl, SigdnNormalDisplay);

    /// <summary>
    /// The name the shell shows for the location of a parsing name (a path, <c>shell:</c>, <c>::{CLSID}</c>), or
    /// <c>null</c> if it does not parse. STA thread.
    /// </summary>
    public static string? DisplayNameOf(string parsingName)
    {
        var hr = SHParseDisplayName(parsingName, 0, out var pidl, 0, out _);
        if (pidl == 0)
        {
            return null;
        }

        try
        {
            return hr == S_OK ? DisplayName(pidl) : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    /// <summary>Reads then frees a <c>CoTaskMemAlloc</c> string.</summary>
    public static string? TakeString(nint text)
    {
        if (text == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(text);
        }
        finally
        {
            Marshal.FreeCoTaskMem(text);
        }
    }

    private static string? Name(nint pidl, uint sigdn)
    {
        if (pidl == 0 || SHGetNameFromIDList(pidl, sigdn, out var name) != S_OK)
        {
            return null;
        }

        return TakeString(name);
    }

    /// <summary>Releases a COM object held by a runtime callable wrapper, if any.</summary>
    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
