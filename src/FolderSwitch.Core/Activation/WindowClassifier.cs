// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>
/// Decides whether a window is a File Explorer window, a file dialog, a web browser, or anything else. Pure logic:
/// the Windows layer gathers the <see cref="WindowInfo"/>.
/// </summary>
/// <remarks>
/// Win32 class names are compared exactly (ordinal); the process name is compared case-insensitively
/// and may carry a <c>.exe</c> suffix.
/// </remarks>
public static class WindowClassifier
{
    private const string ExplorerProcess = "explorer";
    private const string ExecutableSuffix = ".exe";
    private const string DialogClass = "#32770";
    private const string ShellViewClass = "SHELLDLL_DefView";
    private const string TreeViewClass = "SysTreeView32";
    private const string StaticClass = "Static";
    private const string FolderPickerTreeClass = "SHBrowseForFolder ShellNameSpace Control";
    private const string FirefoxClass = "MozillaWindowClass";
    private const string ChromiumClass = "Chrome_WidgetWin_1";

    /// <summary>
    /// Control ID of the tree of the legacy folder picker without <c>BIF_NEWDIALOGSTYLE</c> (shell32's
    /// <c>IDD_FOLDERLIST</c>, 0x3741).
    /// </summary>
    public const int ClassicFolderPickerTreeId = 0x3741;

    /// <summary>Control ID of the title text of the legacy folder picker (<c>IDD_BROWSETITLE</c>).</summary>
    public const int ClassicFolderPickerTitleId = 0x3742;

    /// <summary>Control ID of the status text of the legacy folder picker (<c>IDD_BROWSESTATUS</c>).</summary>
    public const int ClassicFolderPickerStatusId = 0x3743;

    public static CallContextKind Classify(WindowInfo window)
    {
        if (IsExplorerClass(window.ClassName) && IsProcess(window.ProcessName, ExplorerProcess))
        {
            return CallContextKind.Explorer;
        }

        // A message box or a property sheet is a #32770 too: only these descendants make a file dialog.
        // SHELLDLL_DefView: Open / Save As (modern and legacy) and the modern folder picker.
        // Otherwise, a legacy folder picker.
        if (IsDialog(window) && (HasDescendant(window, ShellViewClass) || IsLegacyFolderPicker(window)))
        {
            return CallContextKind.FileDialog;
        }

        return BrowserOf(window) is null ? CallContextKind.Other : CallContextKind.Browser;
    }

    /// <summary>
    /// The browser a window is the main window of, <c>null</c> if it is not one: Firefox is process <c>firefox</c>
    /// with class <c>MozillaWindowClass</c>, Chrome <c>chrome</c> and Edge <c>msedge</c> with <c>Chrome_WidgetWin_1</c>.
    /// Both conditions are needed: Chrome's notifications and other applications built on Chromium (Electron)
    /// share the class or the process name, not both.
    /// </summary>
    public static BrowserKind? BrowserOf(WindowInfo window)
    {
        if (string.Equals(window.ClassName, FirefoxClass, StringComparison.Ordinal))
        {
            return IsProcess(window.ProcessName, "firefox") ? BrowserKind.Firefox : null;
        }

        if (string.Equals(window.ClassName, ChromiumClass, StringComparison.Ordinal))
        {
            if (IsProcess(window.ProcessName, "chrome"))
            {
                return BrowserKind.Chrome;
            }

            if (IsProcess(window.ProcessName, "msedge"))
            {
                return BrowserKind.Edge;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the window is a legacy folder picker (<c>SHBrowseForFolder</c>), the dialogs that take
    /// <c>BFFM_*</c> messages:
    /// <list type="bullet">
    /// <item>new dialog style: a <c>#32770</c> with a <c>SysTreeView32</c> and the
    /// <c>SHBrowseForFolder ShellNameSpace Control</c> that hosts it;</item>
    /// <item>classic style, which has no distinctive class: a <c>#32770</c> with the shell32 template's
    /// controls, all three with their control IDs, a <c>SysTreeView32</c> 0x3741 (folder list) and two
    /// <c>Static</c> 0x3742 and 0x3743 (title and status texts).</item>
    /// </list>
    /// </summary>
    public static bool IsLegacyFolderPicker(WindowInfo window) =>
        IsDialog(window)
        && ((HasDescendant(window, TreeViewClass) && HasDescendant(window, FolderPickerTreeClass))
            || (HasDescendant(window, TreeViewClass, ClassicFolderPickerTreeId)
                && HasDescendant(window, StaticClass, ClassicFolderPickerTitleId)
                && HasDescendant(window, StaticClass, ClassicFolderPickerStatusId)));

    private static bool IsDialog(WindowInfo window) => string.Equals(window.ClassName, DialogClass, StringComparison.Ordinal);

    private static bool IsExplorerClass(string className) =>
        string.Equals(className, "CabinetWClass", StringComparison.Ordinal)
        || string.Equals(className, "ExploreWClass", StringComparison.Ordinal);

    private static bool IsProcess(string processName, string expected)
    {
        var name = processName.EndsWith(ExecutableSuffix, StringComparison.OrdinalIgnoreCase)
            ? processName[..^ExecutableSuffix.Length]
            : processName;

        return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDescendant(WindowInfo window, string className) =>
        window.DescendantClassNames.Contains(className, StringComparer.Ordinal);

    /// <summary>A descendant of that class with that control ID (the two lists are read in parallel).</summary>
    private static bool HasDescendant(WindowInfo window, string className, int controlId)
    {
        var count = Math.Min(window.DescendantClassNames.Count, window.DescendantControlIds.Count);
        for (var i = 0; i < count; i++)
        {
            if (window.DescendantControlIds[i] == controlId
                && string.Equals(window.DescendantClassNames[i], className, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
