// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Updates;

namespace FolderSwitch.Core.Tests.Updates;

public class UpdateAnnouncementsTests
{
    private static readonly Version V011 = new(0, 1, 1);
    private static readonly Version V012 = new(0, 1, 2);
    private static readonly Version V020 = new(0, 2, 0);

    [Fact]
    public void Nothing_is_announced_at_first()
    {
        var announcements = new UpdateAnnouncements<Version>();

        Assert.Null(announcements.Announced);
    }

    [Fact]
    public void The_first_version_found_is_announced()
    {
        var announcements = new UpdateAnnouncements<Version>();

        Assert.True(announcements.TryAnnounce(V011));
        Assert.Equal(V011, announcements.Announced);
    }

    [Fact]
    public void The_same_version_is_not_announced_twice()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.TryAnnounce(V011);

        Assert.False(announcements.TryAnnounce(new Version(0, 1, 1)));
        Assert.Equal(V011, announcements.Announced);
    }

    [Fact]
    public void A_newer_version_is_announced_again()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.TryAnnounce(V011);

        Assert.True(announcements.TryAnnounce(V012));
        Assert.Equal(V012, announcements.Announced);

        // And the one announced after that must again be newer than this one.
        Assert.False(announcements.TryAnnounce(V012));
        Assert.True(announcements.TryAnnounce(V020));
    }

    [Fact]
    public void An_older_version_is_not_announced_and_does_not_lower_the_bar()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.TryAnnounce(V012);

        // A release withdrawn from the feed, say: the user was told about 0.1.2 already.
        Assert.False(announcements.TryAnnounce(V011));
        Assert.Equal(V012, announcements.Announced);
        Assert.False(announcements.TryAnnounce(V012));
    }

    [Fact]
    public void A_null_version_is_refused()
    {
        var announcements = new UpdateAnnouncements<Version>();

        Assert.Throws<ArgumentNullException>(() => announcements.TryAnnounce(null!));
    }
}
