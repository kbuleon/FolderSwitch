// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>
/// The file selected in the active tab of a File Explorer window, as its « Selected file » may offer it: a definite answer
/// of <see cref="ICurrentFolderReader.TryReadSelection"/>.
/// </summary>
/// <remarks>
/// <para>
/// A file is offered when exactly one item is selected and it is a file: an item with a file-system path that is not a
/// folder. An item that is both a folder and a file (a <c>.zip</c>) counts as a file. Any view offers it, search results
/// and Home included, except the Recycle Bin's.
/// </para>
/// <para>
/// A shell shortcut (<c>.lnk</c>) counts by its target: the target's path is offered when it is an existing file, and
/// nothing is when it is a folder, is missing or cannot be read. A file that is not a shell link (an Internet shortcut
/// <c>.url</c>...) is a plain file.
/// </para>
/// </remarks>
/// <param name="FilePath">
/// The path of the file to offer (of its target, for a shortcut), or <c>null</c> when there is nothing to offer: nothing or
/// several items selected, a folder, an item without a file-system path, a view of the Recycle Bin.
/// </param>
public sealed record ExplorerSelection(string? FilePath)
{
    /// <summary>Nothing to offer: a definite answer, unlike a read that failed.</summary>
    public static ExplorerSelection NoFile { get; } = new((string?)null);
}
