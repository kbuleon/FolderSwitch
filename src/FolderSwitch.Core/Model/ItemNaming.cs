// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.SpecialFolders;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Model;

/// <summary>Computes the name shown for an <see cref="Item"/>.</summary>
public static class ItemNaming
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>
    /// The user's <see cref="Item.Name"/> when it is not blank; otherwise the translated name of the
    /// special folder; otherwise, for a web site, its host without <c>www.</c>
    /// (<see cref="WebAddress.HostLabel"/>); otherwise the last segment of the path (<c>D:\Clients\Acme</c> gives <c>Acme</c>,
    /// <c>\\nas\partage</c> gives <c>partage</c>, a drive root such as <c>D:\</c> is kept as is).
    /// Empty when the item has no name, no special folder and no path.
    /// </summary>
    /// <param name="item">The item to name.</param>
    /// <param name="localize">Maps a localization key (for example <c>SpecialFolder.ThisPC</c>) to its text.</param>
    public static string DisplayName(Item item, Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localize);

        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name;
        }

        if (item.Special is { } special)
        {
            return localize(SpecialFolderCatalog.NameKeyFor(special));
        }

        if (item.Kind == ItemKind.Url && !string.IsNullOrWhiteSpace(item.Url))
        {
            return WebAddress.HostLabel(item.Url);
        }

        return LastSegment(item.Path);
    }

    private static string LastSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.TrimEnd(Separators);
        if (trimmed.Length == 0)
        {
            // Nothing but separators ("\" or "/"): there is no segment to extract.
            return path;
        }

        if (IsDriveRoot(trimmed))
        {
            return path;
        }

        var lastSeparator = trimmed.LastIndexOfAny(Separators);
        return lastSeparator < 0 ? trimmed : trimmed[(lastSeparator + 1)..];
    }

    private static bool IsDriveRoot(string trimmed) =>
        trimmed.Length == 2 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':';
}
