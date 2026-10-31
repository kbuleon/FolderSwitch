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

    // The item's menu: « Open in File Explorer ».

    [Fact]
    public void In_Explorer_a_folder_opens_in_a_new_window()
    {
        Assert.Equal(
            new ActivationPlan(ActivationKind.OpenNewExplorerWindow, @"C:\Work", null),
            ActivationRouter.PlanInExplorer(ItemKind.Folder, @"C:\Work"));
    }

    [Theory]
    [InlineData(@"C:\Work\a.txt", @"C:\Work")]
    [InlineData(@"\\server\share\b.docx", @"\\server\share")]
    public void In_Explorer_a_file_opens_its_folder_in_a_new_window(string file, string folder)
    {
        Assert.Equal(
            new ActivationPlan(ActivationKind.OpenNewExplorerWindow, folder, null),
            ActivationRouter.PlanInExplorer(ItemKind.File, file));
    }

    // The item's menu: « Open in a new tab » is offered exactly when Ctrl+click opens one.

    [Theory]
    [InlineData(CallContextKind.Explorer, ItemKind.Folder, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Explorer, ItemKind.Folder, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.Explorer, ItemKind.File, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.FileDialog, ItemKind.Folder, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Other, ItemKind.Folder, WindowsGeneration.Windows11)]
    public void CtrlOpensNewTab_agrees_with_the_plan_of_a_ctrl_click(CallContextKind context, ItemKind kind, WindowsGeneration os)
    {
        var ctx = new CallContext(context, 42, null);
        var ctrlClick = ActivationRouter.Plan(ctx, kind, @"C:\Work", ctrl: true, os);

        Assert.Equal(ctrlClick.Kind == ActivationKind.OpenExplorerNewTab, ActivationRouter.CtrlOpensNewTab(ctx, kind, os));
    }

    [Fact]
    public void CtrlOpensNewTab_only_for_a_folder_called_from_Explorer_on_Windows11()
    {
        Assert.True(ActivationRouter.CtrlOpensNewTab(new CallContext(CallContextKind.Explorer, 42, null), ItemKind.Folder, WindowsGeneration.Windows11));
        Assert.False(ActivationRouter.CtrlOpensNewTab(new CallContext(CallContextKind.Explorer, 42, null), ItemKind.Folder, WindowsGeneration.Windows10));
        Assert.False(ActivationRouter.CtrlOpensNewTab(CallContext.None, ItemKind.Folder, WindowsGeneration.Windows11));
    }
}
