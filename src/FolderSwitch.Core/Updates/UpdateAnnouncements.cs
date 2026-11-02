// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Updates;

/// <summary>
/// Remembers the newest version that was announced to the user (notification, menu entry), so that the periodic
/// check announces a version once, and announces again only for a newer one: a user who answered « Later » is not
/// reminded of the same version every 24 hours.
/// </summary>
/// <typeparam name="TVersion">
/// The version type of the update library (its own semantic version, so that pre-releases order correctly); only
/// its order is used.
/// </typeparam>
/// <remarks>The state lives for the process only: a restart announces the pending version again, once.</remarks>
public sealed class UpdateAnnouncements<TVersion>
    where TVersion : class, IComparable<TVersion>
{
    /// <summary>The newest version announced so far; <c>null</c> before the first announcement.</summary>
    public TVersion? Announced { get; private set; }

    /// <summary>
    /// Records <paramref name="found"/> as announced when nothing was announced yet or when it is newer than the
    /// version announced so far.
    /// </summary>
    /// <returns>
    /// <c>true</c> when <paramref name="found"/> is new to the user and must be announced; <c>false</c> when this
    /// version, or a newer one, was announced already.
    /// </returns>
    public bool TryAnnounce(TVersion found)
    {
        ArgumentNullException.ThrowIfNull(found);

        if (Announced is not null && found.CompareTo(Announced) <= 0)
        {
            return false;
        }

        Announced = found;
        return true;
    }
}
