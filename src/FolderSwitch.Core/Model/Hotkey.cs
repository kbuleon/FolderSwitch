// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text.Json.Serialization;

namespace FolderSwitch.Core.Model;

/// <summary>
/// The global shortcut. When <see cref="VirtualKey"/> is <c>null</c> the shortcut is made of
/// modifier keys only and fires when they are released.
/// </summary>
public sealed record Hotkey(KeyModifiers Modifiers, int? VirtualKey)
{
    public static Hotkey Default { get; } = new(KeyModifiers.Ctrl | KeyModifiers.Win, null);

    /// <summary>Derived from <see cref="VirtualKey"/>; never persisted.</summary>
    [JsonIgnore]
    public bool IsModifierOnly => VirtualKey is null;
}
