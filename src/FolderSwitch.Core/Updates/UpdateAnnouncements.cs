// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Updates;

/// <summary>What to do, now, about the update that the periodic check found (see <see cref="UpdateAnnouncements{TVersion}.Decide"/>).</summary>
public enum AutomaticOffer
{
    /// <summary>Nothing is waiting to be offered.</summary>
    None,

    /// <summary>FolderSwitch is not in use: the « Install / Later » box opens now.</summary>
    Open,

    /// <summary>FolderSwitch is in use, or another update operation runs: the box waits, and is tried again later.</summary>
    Postpone,
}

/// <summary>
/// Remembers the newest version whose « Install / Later » box was opened (<see cref="Announced"/>), so that the box
/// opens once per version in a session and again only for a newer one: a user who answered « Later » is not reminded of
/// the same version every 24 hours. It also holds the version that the periodic check found and that waits for its box
/// (<see cref="Pending"/>): the box never opens while FolderSwitch is in use (<see cref="Decide"/>).
/// </summary>
/// <typeparam name="TVersion">
/// The version type of the update library (its own semantic version, so that pre-releases order correctly); only
/// its order is used.
/// </typeparam>
/// <remarks>The state lives for the process only: a restart announces the pending version again, once.</remarks>
public sealed class UpdateAnnouncements<TVersion>
    where TVersion : class, IComparable<TVersion>
{
    /// <summary>The newest version whose box was opened so far; <c>null</c> before the first one.</summary>
    public TVersion? Announced { get; private set; }

    /// <summary>
    /// The version found by the periodic check that is new to the user and whose box did not open yet; <c>null</c> when
    /// none waits.
    /// </summary>
    public TVersion? Pending { get; private set; }

    /// <summary>Whether no box was opened yet for <paramref name="found"/> or for a newer version.</summary>
    public bool IsNew(TVersion found)
    {
        ArgumentNullException.ThrowIfNull(found);

        return Announced is null || found.CompareTo(Announced) > 0;
    }

    /// <summary>
    /// The periodic check found <paramref name="found"/>: when it is new to the user (<see cref="IsNew"/>), it waits for
    /// its box (<see cref="Pending"/>, replacing an older one); otherwise nothing waits any more.
    /// </summary>
    /// <returns>Whether <paramref name="found"/> now waits for its box.</returns>
    public bool Hold(TVersion found)
    {
        Pending = IsNew(found) ? found : null;
        return Pending is not null;
    }

    /// <summary>The last check found no update: nothing waits any more (<see cref="Announced"/> is kept).</summary>
    public void Drop() => Pending = null;

    /// <summary>
    /// The box of <paramref name="offered"/> opens, whoever opened it (the periodic check, the menu entry, the check asked
    /// in the settings): records it as announced when it is new, and drops a pending version that is not newer.
    /// </summary>
    /// <returns>
    /// <c>true</c> when <paramref name="offered"/> was new to the user; <c>false</c> when this version, or a newer one,
    /// was announced already.
    /// </returns>
    public bool TryAnnounce(TVersion offered)
    {
        var isNew = IsNew(offered);
        if (isNew)
        {
            Announced = offered;
        }

        if (Pending is not null && Pending.CompareTo(offered) <= 0)
        {
            Pending = null;
        }

        return isNew;
    }

    /// <summary>
    /// Whether the box of the pending version may open now: never while FolderSwitch is in use (<paramref name="inUse"/>:
    /// a window, a box or a picker of it on screen), nor while another update operation runs (<paramref name="busy"/>).
    /// </summary>
    public AutomaticOffer Decide(bool busy, bool inUse) =>
        Pending is null ? AutomaticOffer.None
        : busy || inUse ? AutomaticOffer.Postpone
        : AutomaticOffer.Open;
}
