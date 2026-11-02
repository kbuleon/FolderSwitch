// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

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

    /// <summary>The items « Open in File Explorer » was asked for.</summary>
    public List<Item> ExplorerCalls { get; } = new();

    /// <summary>The answer of <see cref="CtrlOpensNewTab"/>, for any item and context.</summary>
    public bool NewTabPossible { get; set; }

    public Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl)
    {
        Calls.Add((item, ctx, ctrl));
        return Pending ?? Task.FromResult(Outcome);
    }

    public Task<ActivationOutcome> OpenInExplorerAsync(Item item)
    {
        ExplorerCalls.Add(item);
        return Pending ?? Task.FromResult(Outcome);
    }

    public bool CtrlOpensNewTab(ItemKind kind, CallContext ctx) => NewTabPossible;
}
