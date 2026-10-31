using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Activation;

public class ActivationRouterTests
{
    private static ActivationPlan Plan(
        CallContextKind context,
        ItemKind kind,
        string path,
        bool ctrl,
        WindowsGeneration os) =>
        ActivationRouter.Plan(new CallContext(context, 42, null), kind, path, ctrl, os);

    // Spec 5.3: Explorer context, folder.

    [Theory]
    [InlineData(WindowsGeneration.Windows10)]
    [InlineData(WindowsGeneration.Windows11)]
    public void Explorer_folder_click_navigates_the_explorer_window(WindowsGeneration os)
    {
        var plan = Plan(CallContextKind.Explorer, ItemKind.Folder, @"C:\Work", ctrl: false, os);

        // The calling window may have gone meanwhile: a new window then.
        Assert.Equal(new ActivationPlan(ActivationKind.NavigateExplorer, @"C:\Work", ActivationKind.OpenNewExplorerWindow), plan);
    }

    [Fact]
    public void Explorer_folder_ctrl_click_on_Windows11_tries_a_new_tab_then_falls_back_to_a_new_window()
    {
        var plan = Plan(CallContextKind.Explorer, ItemKind.Folder, @"C:\Work", ctrl: true, WindowsGeneration.Windows11);

        Assert.Equal(
            new ActivationPlan(ActivationKind.OpenExplorerNewTab, @"C:\Work", ActivationKind.OpenNewExplorerWindow),
            plan);
    }

    [Fact]
    public void Explorer_folder_ctrl_click_on_Windows10_opens_a_new_window()
    {
        var plan = Plan(CallContextKind.Explorer, ItemKind.Folder, @"C:\Work", ctrl: true, WindowsGeneration.Windows10);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenNewExplorerWindow, @"C:\Work", null), plan);
    }

    // Spec 5.3: file dialog context, folder (Ctrl is ignored).

    [Theory]
    [InlineData(false, WindowsGeneration.Windows10)]
    [InlineData(true, WindowsGeneration.Windows10)]
    [InlineData(false, WindowsGeneration.Windows11)]
    [InlineData(true, WindowsGeneration.Windows11)]
    public void FileDialog_folder_click_moves_the_dialog_to_the_folder(bool ctrl, WindowsGeneration os)
    {
        var plan = Plan(CallContextKind.FileDialog, ItemKind.Folder, @"D:\a", ctrl, os);

        Assert.Equal(new ActivationPlan(ActivationKind.NavigateFileDialog, @"D:\a", null), plan);
    }

    // Spec 5.3: file in a file dialog goes to the file's folder (Ctrl is ignored).

    [Theory]
    [InlineData(@"D:\a\b.txt", @"D:\a")]
    [InlineData(@"D:\a\b\c.docx", @"D:\a\b")]
    [InlineData(@"D:\b.txt", @"D:\")]
    [InlineData(@"\\srv\share\dir\f.txt", @"\\srv\share\dir")]
    [InlineData(@"\\srv\share\f.txt", @"\\srv\share")]
    public void FileDialog_file_click_moves_the_dialog_to_the_parent_folder(string file, string expectedFolder)
    {
        foreach (var ctrl in new[] { false, true })
        {
            foreach (var os in new[] { WindowsGeneration.Windows10, WindowsGeneration.Windows11 })
            {
                var plan = Plan(CallContextKind.FileDialog, ItemKind.File, file, ctrl, os);

                Assert.Equal(new ActivationPlan(ActivationKind.NavigateFileDialog, expectedFolder, null), plan);
            }
        }
    }

    [Theory]
    [InlineData("shell:Downloads")]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    public void FileDialog_file_with_a_shell_form_keeps_its_target_as_is(string target)
    {
        var plan = Plan(CallContextKind.FileDialog, ItemKind.File, target, ctrl: false, WindowsGeneration.Windows11);

        Assert.Equal(new ActivationPlan(ActivationKind.NavigateFileDialog, target, null), plan);
    }

    // Spec 5.3: other context, folder.

    [Theory]
    [InlineData(false, WindowsGeneration.Windows10)]
    [InlineData(true, WindowsGeneration.Windows10)]
    [InlineData(false, WindowsGeneration.Windows11)]
    [InlineData(true, WindowsGeneration.Windows11)]
    public void Other_folder_click_opens_a_new_explorer_window(bool ctrl, WindowsGeneration os)
    {
        var plan = Plan(CallContextKind.Other, ItemKind.Folder, @"C:\Work", ctrl, os);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenNewExplorerWindow, @"C:\Work", null), plan);
    }

    [Theory]
    [InlineData("shell:MyComputerFolder")]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData(@"\\srv\share\folder")]
    public void Other_virtual_or_unc_folder_opens_a_new_explorer_window_on_the_exact_target(string target)
    {
        var plan = Plan(CallContextKind.Other, ItemKind.Folder, target, ctrl: false, WindowsGeneration.Windows11);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenNewExplorerWindow, target, null), plan);
    }

    // Spec 5.3: file in the Explorer or in another context opens with the associated application.

    [Theory]
    [InlineData(CallContextKind.Other, false, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.Other, true, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Explorer, false, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.Explorer, true, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.Explorer, false, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Explorer, true, WindowsGeneration.Windows11)]
    public void File_click_outside_a_file_dialog_opens_the_file(CallContextKind context, bool ctrl, WindowsGeneration os)
    {
        var plan = Plan(context, ItemKind.File, @"D:\a\b.txt", ctrl, os);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenFile, @"D:\a\b.txt", null), plan);
    }

    [Fact]
    public void Undefined_context_is_treated_as_Other()
    {
        var folder = Plan((CallContextKind)99, ItemKind.Folder, @"C:\Work", ctrl: true, WindowsGeneration.Windows11);
        var file = Plan((CallContextKind)99, ItemKind.File, @"C:\Work\a.txt", ctrl: true, WindowsGeneration.Windows11);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenNewExplorerWindow, @"C:\Work", null), folder);
        Assert.Equal(new ActivationPlan(ActivationKind.OpenFile, @"C:\Work\a.txt", null), file);
    }
}
