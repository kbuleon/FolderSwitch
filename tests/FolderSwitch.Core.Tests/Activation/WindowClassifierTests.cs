// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;

namespace FolderSwitch.Core.Tests.Activation;

public class WindowClassifierTests
{
    private static readonly string[] None = [];

    public static TheoryData<string, string, string[], CallContextKind> Cases => new()
    {
        // Explorer windows (classic and "explore" classes), process name with or without ".exe".
        { "CabinetWClass", "explorer", ["ShellTabWindowClass", "DirectUIHWND", "SHELLDLL_DefView"], CallContextKind.Explorer },
        { "ExploreWClass", "explorer", None, CallContextKind.Explorer },
        { "CabinetWClass", "explorer.exe", None, CallContextKind.Explorer },
        { "CabinetWClass", "Explorer", None, CallContextKind.Explorer },
        { "CabinetWClass", "EXPLORER.EXE", None, CallContextKind.Explorer },

        // Explorer class but another process: not the Explorer.
        { "CabinetWClass", "notepad", ["SHELLDLL_DefView"], CallContextKind.Other },
        { "CabinetWClass", "explorer2", None, CallContextKind.Other },
        { "CabinetWClass", "", None, CallContextKind.Other },

        // Class names are compared exactly (ordinal).
        { "cabinetwclass", "explorer", None, CallContextKind.Other },
        { "#32770", "winword", ["shelldll_defview"], CallContextKind.Other },

        // Modern Open / Save As dialog and modern folder picker.
        { "#32770", "notepad", ["DUIViewWndClassName", "DirectUIHWND", "SHELLDLL_DefView"], CallContextKind.FileDialog },
        // Legacy Open / Save As dialog.
        { "#32770", "legacyapp", ["ComboBoxEx32", "SHELLDLL_DefView", "Button", "Edit"], CallContextKind.FileDialog },
        // Legacy folder picker (SHBrowseForFolder).
        { "#32770", "legacyapp", ["SHBrowseForFolder ShellNameSpace Control", "SysTreeView32", "Button"], CallContextKind.FileDialog },

        // Message box and property sheet: #32770 without the file-dialog descendants.
        { "#32770", "notepad", ["Button", "Static"], CallContextKind.Other },
        { "#32770", "notepad", None, CallContextKind.Other },
        // A bare SysTreeView32 or a bare SHBrowseForFolder control is not enough for the legacy picker.
        { "#32770", "legacyapp", ["SysTreeView32", "Button"], CallContextKind.Other },
        { "#32770", "legacyapp", ["SHBrowseForFolder ShellNameSpace Control"], CallContextKind.Other },

        // File-dialog descendants in a window that is not a #32770 dialog.
        { "Notepad", "notepad", ["SHELLDLL_DefView"], CallContextKind.Other },

        // The browsers: process and class together.
        { "MozillaWindowClass", "firefox", None, CallContextKind.Browser },
        { "Chrome_WidgetWin_1", "chrome", None, CallContextKind.Browser },
        { "Chrome_WidgetWin_1", "msedge", None, CallContextKind.Browser },
        { "MozillaWindowClass", "firefox.EXE", None, CallContextKind.Browser },
        { "Chrome_WidgetWin_1", "CHROME.exe", None, CallContextKind.Browser },
        // A browser process with another class (a notification, a tooltip), or a browser class in another process.
        { "NotificationWindow", "chrome", None, CallContextKind.Other },
        { "Chrome_WidgetWin_1", "slack", None, CallContextKind.Other },
        { "MozillaWindowClass", "thunderbird", None, CallContextKind.Other },
        { "Chrome_WidgetWin_1", "firefox", None, CallContextKind.Other },
        { "MozillaWindowClass", "chrome", None, CallContextKind.Other },
        { "chrome_widgetwin_1", "chrome", None, CallContextKind.Other },
        // A browser's file dialog is a dialog, not the browser.
        { "#32770", "chrome", ["SHELLDLL_DefView"], CallContextKind.FileDialog },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Classify_applies_the_window_rules(
        string className,
        string processName,
        string[] descendants,
        CallContextKind expected)
    {
        var window = new WindowInfo(className, processName, descendants);

        Assert.Equal(expected, WindowClassifier.Classify(window));
    }

    /// <summary>
    /// Real windows, as WindowInspector reads them (Windows 11 build 26300, fr-FR): distinct class
    /// names breadth first, with the control IDs where a rule needs them (0 = none).
    /// </summary>
    public static TheoryData<string, string, string[], int[], CallContextKind> RealWindows => new()
    {
        // Explorer window (Windows 11, with tabs).
        {
            "CabinetWClass", "explorer",
            ["TITLE_BAR_SCAFFOLDING_WINDOW_CLASS", "ShellTabWindowClass", "Microsoft.UI.Content.DesktopChildSiteBridge", "InputNonClientPointerSource", "DUIViewWndClassName", "msctls_statusbar32", "InputSiteWindowClass", "DirectUIHWND", "CtrlNotifySink", "NamespaceTreeControl", "Button", "Shell Preview Extension Host", "SHELLDLL_DefView", "Static", "SysTreeView32"],
            [], CallContextKind.Explorer
        },

        // Common Item Dialog: Open.
        {
            "#32770", "DialogHost",
            ["DUIViewWndClassName", "Static", "ComboBoxEx32", "Button", "ScrollBar", "ListBox", "WorkerW", "DirectUIHWND", "ComboBox", "ReBarWindow32", "CtrlNotifySink", "Edit", "TravelBand", "UpBand", "Address Band Root", "SeparatorBand", "UniversalSearchBand", "NamespaceTreeControl", "Shell Preview Extension Host", "SHELLDLL_DefView", "ToolbarWindow32", "msctls_progress32", "Search Box", "SysTreeView32", "Breadcrumb Parent", "SearchEditBoxWrapperClass"],
            [], CallContextKind.FileDialog
        },

        // Common Item Dialog: Save As.
        {
            "#32770", "DialogHost",
            ["DUIViewWndClassName", "ToolbarWindow32", "Button", "ScrollBar", "Static", "ListBox", "WorkerW", "DirectUIHWND", "ReBarWindow32", "FloatNotifySink", "CtrlNotifySink", "TravelBand", "UpBand", "Address Band Root", "SeparatorBand", "UniversalSearchBand", "ComboBox", "NamespaceTreeControl", "SHELLDLL_DefView", "msctls_progress32", "Search Box", "Edit", "SysTreeView32", "Breadcrumb Parent", "SearchEditBoxWrapperClass"],
            [], CallContextKind.FileDialog
        },

        // Common Item Dialog: folder picker (FOS_PICKFOLDERS).
        {
            "#32770", "DialogHost",
            ["DUIViewWndClassName", "Static", "Edit", "Button", "ScrollBar", "ListBox", "WorkerW", "DirectUIHWND", "ReBarWindow32", "CtrlNotifySink", "TravelBand", "UpBand", "Address Band Root", "SeparatorBand", "UniversalSearchBand", "NamespaceTreeControl", "SHELLDLL_DefView", "ToolbarWindow32", "msctls_progress32", "Search Box", "SysTreeView32", "Breadcrumb Parent", "SearchEditBoxWrapperClass"],
            [], CallContextKind.FileDialog
        },

        // Legacy Explorer-style Open / Save As (comdlg32 with a hook): same classes for both.
        {
            "#32770", "DialogHost",
            ["Static", "ComboBox", "ToolbarWindow32", "ListBox", "SHELLDLL_DefView", "ComboBoxEx32", "Button", "ScrollBar", "#32770", "SysListView32", "SysHeader32", "Edit"],
            [], CallContextKind.FileDialog
        },

        // Legacy folder picker, new dialog style: a Static carries the IDD_FOLDERLIST ID, the tree is hosted.
        {
            "#32770", "DialogHost",
            ["Static", "Static", "SHBrowseForFolder ShellNameSpace Control", "Static", "Edit", "Static", "Button", "Button", "Button", "ScrollBar", "Static", "SysTreeView32"],
            [0x3742, 0x3741, 0, 0x3748, 0x3744, 0x3749, 0x3746, 1, 2, 0x3747, 0, 0x64],
            CallContextKind.FileDialog
        },

        // Legacy folder picker, classic style: only the tree's IDD_FOLDERLIST ID tells it apart.
        {
            "#32770", "DialogHost",
            ["Static", "Static", "SysTreeView32", "Button", "Button"],
            [0x3742, 0x3743, 0x3741, 1, 2],
            CallContextKind.FileDialog
        },

        // The same classes without the IDs, or with the tree under another ID: an ordinary dialog.
        { "#32770", "DialogHost", ["Static", "Static", "SysTreeView32", "Button", "Button"], [], CallContextKind.Other },
        { "#32770", "settings", ["Static", "Static", "SysTreeView32", "Button", "Button"], [0x3742, 0x3743, 0x3E8, 1, 2], CallContextKind.Other },
        // The ID on a window that is not the tree.
        { "#32770", "settings", ["Static", "SysTreeView32", "Button"], [0x3741, 0x3E8, 1], CallContextKind.Other },
        // The tree ID alone, or with only one of the two texts, or with the text IDs on other classes:
        // not the shell32 template.
        { "#32770", "settings", ["SysTreeView32", "Button", "Button"], [0x3741, 1, 2], CallContextKind.Other },
        { "#32770", "settings", ["Static", "SysTreeView32", "Button"], [0x3742, 0x3741, 1], CallContextKind.Other },
        { "#32770", "settings", ["Static", "SysTreeView32", "Button"], [0x3743, 0x3741, 1], CallContextKind.Other },
        { "#32770", "settings", ["Button", "Edit", "SysTreeView32"], [0x3742, 0x3743, 0x3741], CallContextKind.Other },

        // Classic picker outside a #32770 dialog.
        { "SomeWindow", "app", ["SysTreeView32"], [0x3741], CallContextKind.Other },

        // Message box.
        { "#32770", "testhost", ["Button", "Static"], [], CallContextKind.Other },
    };

    [Theory]
    [MemberData(nameof(RealWindows))]
    public void Classify_recognizes_real_windows(
        string className,
        string processName,
        string[] descendants,
        int[] controlIds,
        CallContextKind expected)
    {
        var window = new WindowInfo(className, processName, descendants) { DescendantControlIds = controlIds };

        Assert.Equal(expected, WindowClassifier.Classify(window));
    }

    public static TheoryData<string, string[], int[], bool> FolderPickers => new()
    {
        // Legacy folder pickers: new dialog style, classic style (real lists).
        {
            "#32770",
            ["Static", "Static", "SHBrowseForFolder ShellNameSpace Control", "Static", "Edit", "Static", "Button", "Button", "Button", "ScrollBar", "Static", "SysTreeView32"],
            [0x3742, 0x3741, 0, 0x3748, 0x3744, 0x3749, 0x3746, 1, 2, 0x3747, 0, 0x64],
            true
        },
        { "#32770", ["Static", "Static", "SysTreeView32", "Button", "Button"], [0x3742, 0x3743, 0x3741, 1, 2], true },

        // File dialogs that are not folder pickers: legacy Open / Save As, Common Item Dialog.
        { "#32770", ["Static", "ComboBox", "ToolbarWindow32", "ListBox", "SHELLDLL_DefView", "ComboBoxEx32", "Button", "#32770", "SysListView32", "Edit"], [], false },
        { "#32770", ["DUIViewWndClassName", "DirectUIHWND", "NamespaceTreeControl", "SHELLDLL_DefView", "SysTreeView32", "Breadcrumb Parent"], [], false },

        // Not the template, not a dialog, a message box.
        { "#32770", ["SysTreeView32", "Button"], [0x3741, 1], false },
        { "SomeWindow", ["Static", "Static", "SysTreeView32"], [0x3742, 0x3743, 0x3741], false },
        { "#32770", ["Button", "Static"], [], false },
    };

    [Theory]
    [MemberData(nameof(FolderPickers))]
    public void IsLegacyFolderPicker_requires_the_picker_structure(
        string className,
        string[] descendants,
        int[] controlIds,
        bool expected)
    {
        var window = new WindowInfo(className, "app", descendants) { DescendantControlIds = controlIds };

        Assert.Equal(expected, WindowClassifier.IsLegacyFolderPicker(window));
    }

    [Fact]
    public void A_shorter_control_id_list_only_pairs_the_first_descendants()
    {
        var window = new WindowInfo("#32770", "app", ["Button", "SysTreeView32"])
        {
            DescendantControlIds = [0x3741],
        };

        Assert.Equal(CallContextKind.Other, WindowClassifier.Classify(window));
    }

    [Fact]
    public void CallContext_None_is_an_Other_context_without_window_or_folder()
    {
        Assert.Equal(CallContextKind.Other, CallContext.None.Kind);
        Assert.Equal(nint.Zero, CallContext.None.Window);
        Assert.Null(CallContext.None.CurrentFolder);
    }

    [Theory]
    [InlineData("MozillaWindowClass", "firefox", BrowserKind.Firefox)]
    [InlineData("MozillaWindowClass", "Firefox.EXE", BrowserKind.Firefox)]
    [InlineData("Chrome_WidgetWin_1", "chrome", BrowserKind.Chrome)]
    [InlineData("Chrome_WidgetWin_1", "chrome.exe", BrowserKind.Chrome)]
    [InlineData("Chrome_WidgetWin_1", "msedge", BrowserKind.Edge)]
    [InlineData("Chrome_WidgetWin_1", "MSEdge.exe", BrowserKind.Edge)]
    [InlineData("NotificationWindow", "chrome", null)]
    [InlineData("Chrome_WidgetWin_1", "explorer", null)]
    [InlineData("Chrome_WidgetWin_1", "", null)]
    [InlineData("MozillaWindowClass", "chrome", null)]
    [InlineData("CabinetWClass", "explorer", null)]
    public void BrowserOf_reads_the_process_and_the_class(string className, string processName, BrowserKind? expected) =>
        Assert.Equal(expected, WindowClassifier.BrowserOf(new WindowInfo(className, processName, None)));
}
