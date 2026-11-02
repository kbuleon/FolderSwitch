// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>
/// An activation that did not go as planned, for the debug log: how it ended, what was tried and why it did not work.
/// Reported by <see cref="ActivationService"/> for every outcome but a plain success, including a failed action that its
/// fallback made up for.
/// </summary>
/// <param name="Outcome">How the activation ended.</param>
/// <param name="Reason">What went wrong, in a few words of English.</param>
/// <param name="Target">The location the item resolved to, when it resolved.</param>
/// <param name="Plan">The plan that ran, when the activation got that far.</param>
/// <param name="Error">The exception thrown, if any.</param>
public sealed record ActivationProblem(
    ActivationOutcome Outcome,
    string Reason,
    string? Target,
    ActivationPlan? Plan,
    Exception? Error);
