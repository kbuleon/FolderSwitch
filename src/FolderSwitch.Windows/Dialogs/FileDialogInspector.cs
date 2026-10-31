using System.Buffers.Binary;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Activation;
using FolderSwitch.Windows.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace FolderSwitch.Windows.Dialogs;

/// <summary>
/// Reads the folder a file dialog of another process shows, for each kind of dialog:
/// <list type="bullet">
/// <item>Common Item Dialog (Open, Save As, folder picker; Vista and later): the text of the address
/// bar's breadcrumb toolbar, <c>"Address: C:\Folder"</c>, localized. That is the very text UI
/// Automation exposes as the toolbar's name, read here with a plain <c>WM_GETTEXT</c>.</item>
/// <item>Legacy Explorer-style Open / Save As (comdlg32 with a hook or a template):
/// <c>CDM_GETFOLDERIDLIST</c>, whose ID list gives the parsing name, virtual folders included.</item>
/// <item>Legacy folder picker (<c>SHBrowseForFolder</c>, both styles): the display names of the
/// selected tree item and its ancestors, resolved through the shell namespace.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Call on an STA thread. The window is classified again here (<see cref="WindowClassifier"/>), and
/// nothing is sent to anything but a file dialog. The kind of dialog is told apart before any
/// <c>WM_USER</c>-range message: <c>CDM_GETFOLDERPATH</c> and <c>CDM_GETFOLDERIDLIST</c> share their
/// values with <c>BFFM_SETSELECTIONA</c> / <c>BFFM_SETSELECTIONW</c>, and would move a folder picker,
/// so <c>CDM_*</c> only goes to a dialog that is not a legacy folder picker
/// (<see cref="WindowClassifier.IsLegacyFolderPicker"/>).
/// </para>
/// <para>
/// Every message has a timeout (<see cref="MessageTimeoutMs"/>). After a message that did not
/// complete, nothing more is sent to the dialog in that lookup, and the buffer the message points to is
/// never freed, since the dialog may still write to it when it gets to the message
/// (<see cref="RemoteMemory"/>; for <c>WM_GETTEXT</c>, a local buffer).
/// </para>
/// </remarks>
internal static class FileDialogInspector
{
    /// <summary>Timeout of each message sent to the dialog.</summary>
    public const uint MessageTimeoutMs = 200;

    private const uint WmUser = 0x0400;
    private const uint CdmGetFolderIdList = WmUser + 103;

    private const uint TvFirst = 0x1100;
    private const uint TvmGetNextItem = TvFirst + 10;
    private const uint TvmGetItemW = TvFirst + 62;
    private const int TvgnParent = 3;
    private const int TvgnCaret = 9;
    private const uint TvifText = 0x0001;

    /// <summary>TVITEMW in either layout (56 or 40 bytes), then the text buffer.</summary>
    private const int TreeItemSize = 64;
    private const int TreeTextChars = 260;

    private const int MaxPathChars = 1024;
    private const int MaxTreeDepth = 64;

    private const string BreadcrumbParentClass = "Breadcrumb Parent";
    private const string ToolbarClass = "ToolbarWindow32";
    private const string ShellViewClass = "SHELLDLL_DefView";
    private const string TreeViewClass = "SysTreeView32";
    private const string FolderPickerTreeHostClass = "SHBrowseForFolder ShellNameSpace Control";

    /// <summary>
    /// The folder <paramref name="dialog"/> shows: a path, or a parsing name such as <c>::{CLSID}</c>
    /// for a virtual location. <c>null</c> when unknown (not a file dialog, process out of reach,
    /// dialog not answering, location without a path). Never throws.
    /// </summary>
    public static string? CurrentFolder(nint dialog)
    {
        try
        {
            if (dialog == 0 || !PInvoke.IsWindow((HWND)dialog))
            {
                return null;
            }

            var info = WindowInspector.Snapshot(dialog);
            if (WindowClassifier.Classify(info) != CallContextKind.FileDialog)
            {
                return null;
            }

            // Common Item Dialog: the shell view is deep inside, the address bar is there.
            var breadcrumb = WindowInspector.FindChild(WindowInspector.FindDescendant(dialog, BreadcrumbParentClass), ToolbarClass);
            if (breadcrumb != 0)
            {
                return FromAddressText(WindowText(breadcrumb));
            }

            if (WindowClassifier.IsLegacyFolderPicker(info))
            {
                return FolderPickerTree(dialog) is var tree and not 0 ? FromTree(tree) : null;
            }

            // Legacy Open / Save As: the shell view is a direct child of the dialog.
            return WindowInspector.FindChild(dialog, ShellViewClass) != 0 ? FromCommonDialog(dialog) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The folder named by an address bar text: what follows the localized label (<c>"Address: "</c>,
    /// <c>"Adresse\u00A0: "</c>...). A path is returned as is; a display path such as <c>"This PC"</c> or
    /// <c>"Libraries\Documents"</c> is resolved through the shell namespace (STA thread).
    /// </summary>
    internal static string? FromAddressText(string? text)
    {
        var location = StripAddressLabel(text);
        if (string.IsNullOrEmpty(location))
        {
            return null;
        }

        return IsPath(location)
            ? location
            : ShellNamespace.ResolveDisplayPath(location.Split('\\', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The text after the first colon followed by a space (ASCII or full-width colon). Without such a
    /// label, the text from the first path root (<c>X:\</c> or <c>\\</c>), if any.
    /// </summary>
    internal static string? StripAddressLabel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] is ':' or '\uFF1A' && char.IsWhiteSpace(text[i + 1]))
            {
                return text[(i + 1)..].Trim();
            }
        }

        for (var i = 0; i < text.Length - 1; i++)
        {
            if ((text[i] == '\\' && text[i + 1] == '\\')
                || (i + 2 < text.Length && char.IsAsciiLetter(text[i]) && text[i + 1] == ':' && text[i + 2] == '\\'))
            {
                return text[i..].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// The tree of a legacy folder picker, or zero: hosted by the <c>SHBrowseForFolder ShellNameSpace
    /// Control</c> (new dialog style) or the direct child with the folder list ID (classic style).
    /// The caller has checked the dialog with <see cref="WindowClassifier.IsLegacyFolderPicker"/>.
    /// </summary>
    internal static nint FolderPickerTree(nint dialog)
    {
        var hosted = WindowInspector.FindChild(WindowInspector.FindChild(dialog, FolderPickerTreeHostClass), TreeViewClass);
        if (hosted != 0)
        {
            return hosted;
        }

        var child = WindowInspector.FindChild(dialog, TreeViewClass);
        return child != 0 && PInvoke.GetDlgCtrlID((HWND)child) == WindowClassifier.ClassicFolderPickerTreeId
            ? child
            : 0;
    }

    /// <summary>The size of the dialog's current folder ID list (<c>CDM_GETFOLDERIDLIST</c> with no buffer), or <c>null</c>.</summary>
    internal static nuint? FolderIdListSize(nint dialog) => WindowMessages.Send(dialog, CdmGetFolderIdList, 0, 0, MessageTimeoutMs);

    /// <summary>
    /// The parsing name of the dialog's current folder ID list of <paramref name="size"/> bytes,
    /// fetched into a block of the dialog's process; <c>null</c> if it cannot be read in time (the
    /// block is then left to the dialog).
    /// </summary>
    internal static string? ReadFolderIdList(nint dialog, int size)
    {
        using var remote = RemoteMemory.Open(dialog, size);
        if (remote?.Send(dialog, CdmGetFolderIdList, size, 0, MessageTimeoutMs) is not > 2)
        {
            return null;
        }

        var bytes = new byte[size];
        if (!remote.Read(0, bytes) || !IsIdList(bytes))
        {
            return null;
        }

        var pidl = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pidl, bytes.Length);
            return ShellNative.ParsingName(pidl);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    /// <summary>The selected item of a tree (<c>TVGN_CARET</c>): zero for none, <c>null</c> if it did not answer.</summary>
    internal static nuint? SelectedTreeItem(nint tree) => WindowMessages.Send(tree, TvmGetNextItem, TvgnCaret, 0, MessageTimeoutMs);

    /// <summary>The text of one tree item, through a block of its own; <c>null</c> if it cannot be read in time.</summary>
    internal static string? ReadTreeItemText(nint tree, nuint item)
    {
        using var remote = RemoteMemory.Open(tree, TreeItemSize + (TreeTextChars * sizeof(char)));
        return remote is null ? null : ItemText(remote, tree, item);
    }

    /// <summary>A drive path (<c>C:\...</c>) or a UNC path (<c>\\server\share...</c>).</summary>
    private static bool IsPath(string location) =>
        location.StartsWith(@"\\", StringComparison.Ordinal)
        || (location.Length >= 3 && char.IsAsciiLetter(location[0]) && location[1] == ':' && location[2] == '\\');

    private static string? FromCommonDialog(nint dialog)
    {
        // The ID list rather than CDM_GETFOLDERPATH: it also covers virtual folders, and its bytes do
        // not depend on whether the dialog was opened through the ANSI or the Unicode API.
        return FolderIdListSize(dialog) is { } size and > 2 and < 65536 ? ReadFolderIdList(dialog, (int)size) : null;
    }

    /// <summary>Whether <paramref name="bytes"/> holds a well-formed ID list, terminator included.</summary>
    private static bool IsIdList(ReadOnlySpan<byte> bytes)
    {
        var offset = 0;
        while (offset + sizeof(ushort) <= bytes.Length)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
            if (size == 0)
            {
                return true;
            }

            if (size < sizeof(ushort))
            {
                return false;
            }

            offset += size;
        }

        return false;
    }

    private static string? FromTree(nint tree)
    {
        using var remote = RemoteMemory.Open(tree, TreeItemSize + (TreeTextChars * sizeof(char)));
        if (remote is null)
        {
            return null;
        }

        // Any message that does not answer in time ends the lookup.
        var names = new List<string>();
        var item = SelectedTreeItem(tree);
        while (item is not null and not 0 && names.Count < MaxTreeDepth)
        {
            if (ItemText(remote, tree, item.Value) is not { } name)
            {
                return null;
            }

            names.Add(name);
            item = WindowMessages.Send(tree, TvmGetNextItem, TvgnParent, (nint)item.Value, MessageTimeoutMs);
        }

        if (item is null || names.Count == 0)
        {
            return null;
        }

        names.Reverse();
        return ShellNamespace.ResolveDisplayPath(names);
    }

    private static string? ItemText(RemoteMemory remote, nint tree, nuint item)
    {
        // TVITEMW: mask, hItem, state, stateMask, pszText, cchTextMax... in the 64-bit or 32-bit layout.
        Span<byte> tvItem = stackalloc byte[TreeItemSize];
        tvItem.Clear();
        var text = (ulong)remote.Address + TreeItemSize;
        BinaryPrimitives.WriteUInt32LittleEndian(tvItem, TvifText);
        if (remote.Is32BitProcess)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tvItem[4..], (uint)item);
            BinaryPrimitives.WriteUInt32LittleEndian(tvItem[16..], (uint)text);
            BinaryPrimitives.WriteInt32LittleEndian(tvItem[20..], TreeTextChars);
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(tvItem[8..], item);
            BinaryPrimitives.WriteUInt64LittleEndian(tvItem[24..], text);
            BinaryPrimitives.WriteInt32LittleEndian(tvItem[32..], TreeTextChars);
        }

        Span<byte> textBytes = stackalloc byte[TreeTextChars * sizeof(char)];
        textBytes.Clear();
        if (!remote.Write(0, tvItem)
            || !remote.Write(TreeItemSize, textBytes)
            || remote.Send(tree, TvmGetItemW, 0, 0, MessageTimeoutMs) is not 1
            || !remote.Read(TreeItemSize, textBytes))
        {
            return null;
        }

        var chars = MemoryMarshal.Cast<byte, char>(textBytes);
        var end = chars.IndexOf('\0');
        return end > 0 ? new string(chars[..end]) : null;
    }

    /// <summary>The window text, or <c>null</c> for none (see <see cref="WindowMessages.GetText"/>).</summary>
    private static string? WindowText(nint window) =>
        WindowMessages.GetText(window, MaxPathChars, MessageTimeoutMs) is { Length: > 0 } text ? text : null;
}
