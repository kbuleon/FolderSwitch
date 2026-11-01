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

    // Spec 5.3, 5.7: a file opens with its application in every context, a file dialog included
    // (the interface never offers a file there: the guard is in the view model).

    [Theory]
    [InlineData(CallContextKind.Other, false, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.Other, true, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Explorer, false, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.Explorer, true, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.FileDialog, false, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.FileDialog, true, WindowsGeneration.Windows11)]
    public void File_opens_with_its_application_in_every_context(CallContextKind context, bool ctrl, WindowsGeneration os)
    {
        var plan = Plan(context, ItemKind.File, @"D:\a\b.txt", ctrl, os);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenFile, @"D:\a\b.txt", null), plan);
    }

    // Spec 5.7: a web site opens in the default browser, whatever the context.

    [Theory]
    [InlineData(CallContextKind.Other, false)]
    [InlineData(CallContextKind.Other, true)]
    [InlineData(CallContextKind.Explorer, false)]
    [InlineData(CallContextKind.Explorer, true)]
    public void Url_opens_in_default_browser_in_every_context(CallContextKind context, bool ctrl)
    {
        foreach (var os in new[] { WindowsGeneration.Windows10, WindowsGeneration.Windows11 })
        {
            var plan = Plan(context, ItemKind.Url, "https://example.com/", ctrl, os);

            Assert.Equal(new ActivationPlan(ActivationKind.OpenUrl, "https://example.com/", null), plan);
        }
    }

    [Theory]
    [InlineData(CallContextKind.FileDialog, ItemKind.File, false)]
    [InlineData(CallContextKind.FileDialog, ItemKind.Url, false)]
    [InlineData(CallContextKind.FileDialog, ItemKind.Folder, true)]
    [InlineData(CallContextKind.Explorer, ItemKind.File, true)]
    [InlineData(CallContextKind.Explorer, ItemKind.Url, true)]
    [InlineData(CallContextKind.Other, ItemKind.Url, true)]
    [InlineData(CallContextKind.Other, ItemKind.File, true)]
    public void IsUsable_greys_files_and_urls_in_file_dialogs(CallContextKind context, ItemKind kind, bool expected)
    {
        Assert.Equal(expected, ActivationRouter.IsUsable(context, kind));
    }

    // A browser window is like any other window: no folder to navigate, a new Explorer window.

    [Theory]
    [InlineData(false, WindowsGeneration.Windows10)]
    [InlineData(true, WindowsGeneration.Windows11)]
    public void Browser_folder_click_opens_a_new_explorer_window(bool ctrl, WindowsGeneration os)
    {
        var plan = Plan(CallContextKind.Browser, ItemKind.Folder, @"C:\Work", ctrl, os);

        Assert.Equal(new ActivationPlan(ActivationKind.OpenNewExplorerWindow, @"C:\Work", null), plan);
    }

    [Theory]
    [InlineData(ItemKind.Folder)]
    [InlineData(ItemKind.File)]
    [InlineData(ItemKind.Url)]
    public void Every_kind_of_item_is_usable_from_a_browser(ItemKind kind) =>
        Assert.True(ActivationRouter.IsUsable(CallContextKind.Browser, kind));

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

    [Theory]
    [InlineData(CallContextKind.Explorer, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Explorer, WindowsGeneration.Windows10)]
    [InlineData(CallContextKind.FileDialog, WindowsGeneration.Windows11)]
    [InlineData(CallContextKind.Other, WindowsGeneration.Windows11)]
    public void CtrlOpensNewTab_is_false_for_urls(CallContextKind context, WindowsGeneration os)
    {
        Assert.False(ActivationRouter.CtrlOpensNewTab(new CallContext(context, 42, null), ItemKind.Url, os));
    }

    [Fact]
    public void CtrlOpensNewTab_only_for_a_folder_called_from_Explorer_on_Windows11()
    {
        Assert.True(ActivationRouter.CtrlOpensNewTab(new CallContext(CallContextKind.Explorer, 42, null), ItemKind.Folder, WindowsGeneration.Windows11));
        Assert.False(ActivationRouter.CtrlOpensNewTab(new CallContext(CallContextKind.Explorer, 42, null), ItemKind.Folder, WindowsGeneration.Windows10));
        Assert.False(ActivationRouter.CtrlOpensNewTab(CallContext.None, ItemKind.Folder, WindowsGeneration.Windows11));
    }
}
