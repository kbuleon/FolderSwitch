// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Editing;

/// <summary>Outcome of a <see cref="BoardEditor"/> operation that can be refused.</summary>
public readonly record struct EditResult(EditError Error)
{
    public bool Ok => Error == EditError.None;
}
