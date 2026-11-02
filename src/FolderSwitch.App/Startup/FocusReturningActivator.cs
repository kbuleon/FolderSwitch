// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.App.Startup;

/// <summary>
/// The activation service as the main view model sees it: when an item was opened in the calling Explorer window or
/// file dialog, the window controller is told, so that the hide the view model asks for right after brings that
/// window back to the foreground (<see cref="WindowController.ReturnFocusAfterHide"/>).
/// </summary>
/// <remarks>Called on the UI thread; the continuation stays there, before the view model's own.</remarks>
internal sealed class FocusReturningActivator(IItemActivator inner, WindowController controller) : IItemActivator
{
    public async Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl)
    {
        // The call the activation belongs to: by the time it is done, the window may have been shown again.
        var showCount = controller.ShowCount;
        var outcome = await inner.ActivateAsync(item, ctx, ctrl);
        if (outcome == ActivationOutcome.Done)
        {
            controller.ReturnFocusAfterHide(ctx, showCount);
        }

        return outcome;
    }

    /// <summary>Always a new window: what was opened comes to the front, there is no focus to give back.</summary>
    public Task<ActivationOutcome> OpenInExplorerAsync(Item item) => inner.OpenInExplorerAsync(item);

    public bool CtrlOpensNewTab(ItemKind kind, CallContext ctx) => inner.CtrlOpensNewTab(kind, ctx);
}
