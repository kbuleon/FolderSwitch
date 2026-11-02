// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>What to do when an item is activated, as decided by <see cref="ActivationRouter"/>.</summary>
/// <param name="Kind">The action to run.</param>
/// <param name="Target">
/// What the action works on: the folder to navigate to or open, the file to open, or the web address to open.
/// </param>
/// <param name="Fallback">The action to run on the same target if <paramref name="Kind"/> fails, if any.</param>
public sealed record ActivationPlan(ActivationKind Kind, string Target, ActivationKind? Fallback);
