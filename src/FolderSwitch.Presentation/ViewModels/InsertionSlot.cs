// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// The index a drag and drop hands to <see cref="MainViewModel.MoveTab"/>, <see cref="MainViewModel.MoveGroup"/> or
/// <see cref="MainViewModel.MoveItem"/>. The view finds a <em>slot</em>: a gap between two elements of the list under
/// the mouse, from 0 (before the first) to the number of elements (after the last). The view model expects a position
/// in the list as it is once the element moved, which differs from the slot when the element moves down its own list:
/// taken out first, everything after it moves up by one.
/// </summary>
public static class InsertionSlot
{
    /// <summary>The index to move to for a drop in <paramref name="slot"/>.</summary>
    /// <param name="slot">The gap under the mouse, 0 to the number of elements of the target list.</param>
    /// <param name="sourceIndex">Where the element is in the target list; -1 when it comes from another list.</param>
    public static int ToMoveIndex(int slot, int sourceIndex) =>
        sourceIndex >= 0 && slot > sourceIndex ? slot - 1 : Math.Max(0, slot);

    /// <summary>
    /// True when a drop in <paramref name="slot"/> leaves the element where it is: the gap just before or just after it
    /// in its own list.
    /// </summary>
    public static bool IsNoMove(int slot, int sourceIndex) =>
        sourceIndex >= 0 && (slot == sourceIndex || slot == sourceIndex + 1);
}
