// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using CommunityToolkit.Mvvm.ComponentModel;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// A board object the user can rename in Customize mode: a tab, a group or an item.
/// Renaming is driven by <see cref="MainViewModel"/> (<c>BeginRename</c>, <c>CommitRename</c>, <c>CancelRename</c>).
/// </summary>
public abstract partial class RenamableViewModel : ObservableObject
{
    private protected RenamableViewModel()
    {
    }

    /// <summary>True while the name is being edited.</summary>
    [ObservableProperty]
    public partial bool IsRenaming { get; internal set; }

    /// <summary>Id of the model object, as <c>BoardEditor.Rename</c> expects it.</summary>
    internal abstract Guid Id { get; }

    /// <summary>The name as shown, which the rename box starts from.</summary>
    internal abstract string CurrentName { get; }

    /// <summary>Raises the change notifications of the displayed name once the model was renamed.</summary>
    internal abstract void NotifyRenamed();
}
