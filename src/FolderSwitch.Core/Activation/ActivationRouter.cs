using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Activation;

/// <summary>
/// The behaviour table of the spec (5.3): what a click or Ctrl+click on a folder or a file does,
/// according to the calling context and the Windows generation. Pure logic.
/// </summary>
/// <remarks>
/// What is done in the calling Explorer window (navigating it, or opening a tab in it) falls back to a new Explorer
/// window: the calling window may have been closed meanwhile, or may not answer.
/// </remarks>
public static class ActivationRouter
{
    public static ActivationPlan Plan(
        CallContext ctx,
        ItemKind kind,
        string resolvedPath,
        bool ctrl,
        WindowsGeneration os)
    {
        if (kind == ItemKind.File)
        {
            // Selecting the file in an Open dialog is out of scope: the dialog goes to the file's folder.
            return ctx.Kind == CallContextKind.FileDialog
                ? new ActivationPlan(ActivationKind.NavigateFileDialog, ParentFolderOf(resolvedPath), null)
                : new ActivationPlan(ActivationKind.OpenFile, resolvedPath, null);
        }

        return ctx.Kind switch
        {
            CallContextKind.Explorer when !ctrl =>
                new ActivationPlan(
                    ActivationKind.NavigateExplorer,
                    resolvedPath,
                    ActivationKind.OpenNewExplorerWindow),
            CallContextKind.Explorer when os == WindowsGeneration.Windows11 =>
                new ActivationPlan(
                    ActivationKind.OpenExplorerNewTab,
                    resolvedPath,
                    ActivationKind.OpenNewExplorerWindow),
            CallContextKind.FileDialog =>
                new ActivationPlan(ActivationKind.NavigateFileDialog, resolvedPath, null),
            _ => new ActivationPlan(ActivationKind.OpenNewExplorerWindow, resolvedPath, null),
        };
    }

    /// <summary>
    /// « Open in File Explorer » (the item's menu): a new Explorer window on the folder, whatever the calling context;
    /// for a file, on the folder that holds it.
    /// </summary>
    public static ActivationPlan PlanInExplorer(ItemKind kind, string resolvedPath) =>
        new(ActivationKind.OpenNewExplorerWindow, kind == ItemKind.File ? ParentFolderOf(resolvedPath) : resolvedPath, null);

    /// <summary>
    /// Whether a Ctrl+click on an item of <paramref name="kind"/> opens a new tab of the calling window
    /// (<see cref="ActivationKind.OpenExplorerNewTab"/>): a folder, called from Explorer, on Windows 11. The item's menu
    /// offers « Open in a new tab » only then.
    /// </summary>
    public static bool CtrlOpensNewTab(CallContext ctx, ItemKind kind, WindowsGeneration os) =>
        kind == ItemKind.Folder && ctx.Kind == CallContextKind.Explorer && os == WindowsGeneration.Windows11;

    /// <summary>
    /// The folder containing <paramref name="path"/>. A <c>shell:</c> or <c>::{CLSID}</c> form, or a
    /// path with no parent, is returned as is.
    /// </summary>
    private static string ParentFolderOf(string path)
    {
        if (ShellForms.IsShellForm(path))
        {
            return path;
        }

        var parent = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(parent) ? path : parent;
    }
}
