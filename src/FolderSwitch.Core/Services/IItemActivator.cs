using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Services;

/// <summary>Carries out the click (or Ctrl+click) on an item.</summary>
public interface IItemActivator
{
    /// <summary>
    /// Opens <paramref name="item"/> according to the calling context. Never throws: a failure is
    /// reported through the returned <see cref="ActivationOutcome"/>.
    /// </summary>
    Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl);
}
