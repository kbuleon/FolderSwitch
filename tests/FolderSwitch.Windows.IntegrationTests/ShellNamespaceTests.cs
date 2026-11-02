// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.Windows.Dialogs;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// The display-name walk used for the folder pickers and the address bar, against the real shell
/// namespace. Display names are read from the shell, so the tests do not depend on the UI language.
/// </summary>
[Trait("Category", "Desktop")]
public sealed class ShellNamespaceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const string ThisPc = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    [Fact]
    public void A_virtual_folder_resolves_to_its_shell_form()
    {
        var thisPc = DisplayName(ThisPc);

        Assert.Equal(ThisPc, Sta.Run(() => ShellNamespace.ResolveDisplayPath([thisPc])), ignoreCase: true);
    }

    [Fact]
    public void A_tree_path_resolves_to_the_folder_path()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var drive = Path.GetPathRoot(windows)!;
        string[] segments = [DisplayName(""), DisplayName(ThisPc), DisplayName(drive), Path.GetFileName(windows)];
        output.WriteLine(string.Join(" | ", segments));

        Assert.Equal(windows, Sta.Run(() => ShellNamespace.ResolveDisplayPath(segments)), ignoreCase: true);
    }

    /// <summary>
    /// The path a legacy folder picker's tree shows for the Startup folder. "Start Menu" holds the
    /// real "Programs" folder, shown under its localized name ("Programmes" in French), and a hidden
    /// system compatibility junction that parses under that very name: the walk must take the folder.
    /// </summary>
    [Fact]
    public void A_tree_path_through_localized_folders_resolves_to_the_real_folder()
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var drive = Path.GetPathRoot(startup)!;
        var segments = new List<string> { DisplayName(""), DisplayName(ThisPc), DisplayName(drive) };
        var levels = startup[drive.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i <= levels.Length; i++)
        {
            segments.Add(DisplayName(Path.Combine(drive, Path.Combine(levels[..i]))));
        }

        output.WriteLine(string.Join(" | ", segments));

        Assert.Equal(startup, Sta.Run(() => ShellNamespace.ResolveDisplayPath(segments)), ignoreCase: true);
    }

    [Fact]
    public void A_name_matching_nothing_gives_null()
    {
        var thisPc = DisplayName(ThisPc);

        Assert.Null(Sta.Run(() => ShellNamespace.ResolveDisplayPath([thisPc, "No such drive " + Guid.NewGuid()])));
        Assert.Null(Sta.Run(() => ShellNamespace.ResolveDisplayPath([])));
    }

    [Fact]
    public void An_address_text_gives_its_path_or_its_shell_form()
    {
        var thisPc = DisplayName(ThisPc);

        Assert.Equal(@"C:\Users", Sta.Run(() => FileDialogInspector.FromAddressText("Adresse\u00A0: C:\\Users")));
        Assert.Equal(ThisPc, Sta.Run(() => FileDialogInspector.FromAddressText("Address: " + thisPc)), ignoreCase: true);
        Assert.Null(Sta.Run(() => FileDialogInspector.FromAddressText("")));
    }

    [Theory]
    [InlineData("Address: C:\\Users\\Karl", "C:\\Users\\Karl")]
    [InlineData("Adresse\u00A0: C:\\Temp", "C:\\Temp")]
    [InlineData("Adresse : Ce PC", "Ce PC")]
    [InlineData("Address: Libraries\\Documents", "Libraries\\Documents")]
    [InlineData("Address: \\\\server\\share\\folder", "\\\\server\\share\\folder")]
    [InlineData("\u30A2\u30C9\u30EC\u30B9: D:\\", "D:\\")]
    [InlineData("\u5730\u5740\uFF1A E:\\Data", "E:\\Data")]
    [InlineData("C:\\NoLabel", "C:\\NoLabel")]
    [InlineData("Label without separator C:\\Folder", "C:\\Folder")]
    public void The_address_label_is_stripped(string text, string expected)
    {
        Assert.Equal(expected, FileDialogInspector.StripAddressLabel(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Address:")]
    [InlineData("No path here")]
    public void An_address_text_without_location_gives_null(string? text)
    {
        Assert.Null(FileDialogInspector.StripAddressLabel(text));
    }

    /// <summary>The normal display name of a parsing name ("" for the desktop), from the shell.</summary>
    private static string DisplayName(string parsingName) => Sta.Run(() =>
    {
        const uint sigdnNormalDisplay = 0;
        nint pidl;
        if (parsingName.Length == 0)
        {
            // The empty ID list (SHParseDisplayName would read "" as This PC).
            pidl = Marshal.AllocCoTaskMem(sizeof(ushort));
            Marshal.WriteInt16(pidl, 0);
        }
        else
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(parsingName, 0, out pidl, 0, out _));
        }

        try
        {
            Marshal.ThrowExceptionForHR(SHGetNameFromIDList(pidl, sigdnNormalDisplay, out var name));
            return ShellNative.TakeString(name)!;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    });

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(string name, nint bindContext, out nint pidl, uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetNameFromIDList(nint pidl, uint sigdn, out nint name);
}
