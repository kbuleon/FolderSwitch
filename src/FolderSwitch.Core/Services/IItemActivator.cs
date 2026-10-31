using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Services;

/// <summary>Carries out the click (or Ctrl+click) on an item, and the entries of its menu.</summary>
public interface IItemActivator
{
    /// <summary>
    /// Opens <paramref name="item"/> according to the calling context. Never throws: a failure is
    /// reported through the returned <see cref="ActivationOutcome"/>.
    /// </summary>
    Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl);

    /// <summary>
    /// « Open in File Explorer »: opens <paramref name="item"/> in a new Explorer window, whatever the calling context
    /// (a file: the folder that holds it). Never throws, like <see cref="ActivateAsync"/>.
    /// </summary>
    Task<ActivationOutcome> OpenInExplorerAsync(Item item);

    /// <summary>Whether a Ctrl+click on an item of <paramref name="kind"/> opens it in a new tab of the calling window.</summary>
    bool CtrlOpensNewTab(ItemKind kind, CallContext ctx);
}
