using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>Answers every activation with <see cref="Outcome"/>, or with <see cref="Pending"/> when it is set.</summary>
internal sealed class FakeItemActivator : IItemActivator
{
    public ActivationOutcome Outcome { get; set; } = ActivationOutcome.Done;

    /// <summary>When set, activations wait for this task instead of finishing at once.</summary>
    public Task<ActivationOutcome>? Pending { get; set; }

    public List<(Item Item, CallContext Context, bool Ctrl)> Calls { get; } = new();

    public Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl)
    {
        Calls.Add((item, ctx, ctrl));
        return Pending ?? Task.FromResult(Outcome);
    }
}
