// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.SpecialFolders;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>One entry of the « + Folder ▾ » menu of a group.</summary>
/// <param name="Info">The catalog entry.</param>
/// <param name="Name">Its name in the current language.</param>
/// <param name="AlreadyPresent">True when the group already holds this folder: the entry is greyed out and checked.</param>
public sealed record SpecialFolderState(SpecialFolderInfo Info, string Name, bool AlreadyPresent);
