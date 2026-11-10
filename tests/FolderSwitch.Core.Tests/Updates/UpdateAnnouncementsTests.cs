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
        Assert.Throws<ArgumentNullException>(() => announcements.Hold(null!));
    }

    [Fact]
    public void A_version_found_waits_for_its_box_without_being_announced()
    {
        var announcements = new UpdateAnnouncements<Version>();

        Assert.True(announcements.Hold(V011));

        // Postponed: still new to the user, the box has not opened.
        Assert.Equal(V011, announcements.Pending);
        Assert.Null(announcements.Announced);
        Assert.True(announcements.IsNew(V011));
    }

    [Fact]
    public void Opening_the_box_announces_the_version_and_ends_the_wait()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.Hold(V011);

        Assert.True(announcements.TryAnnounce(V011));

        Assert.Null(announcements.Pending);
        Assert.Equal(V011, announcements.Announced);
        Assert.Equal(AutomaticOffer.None, announcements.Decide(busy: false, inUse: false));
    }

    [Fact]
    public void A_version_already_announced_does_not_wait()
    {
        // The user answered the box of the menu entry, then the periodic check finds the same version.
        var announcements = new UpdateAnnouncements<Version>();
        announcements.TryAnnounce(V012);

        Assert.False(announcements.Hold(V012));
        Assert.False(announcements.Hold(V011));
        Assert.Null(announcements.Pending);
    }

    [Fact]
    public void A_newer_version_found_while_one_waits_takes_its_place()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.Hold(V011);

        Assert.True(announcements.Hold(V012));

        Assert.Equal(V012, announcements.Pending);
    }

    [Fact]
    public void A_box_opened_for_an_older_version_leaves_the_newer_one_waiting()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.Hold(V020);

        announcements.TryAnnounce(V012);

        Assert.Equal(V020, announcements.Pending);
    }

    [Fact]
    public void Up_to_date_drops_the_wait_and_keeps_what_was_announced()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.TryAnnounce(V011);
        announcements.Hold(V012);

        announcements.Drop();

        Assert.Null(announcements.Pending);
        Assert.Equal(V011, announcements.Announced);
    }

    [Theory]
    [InlineData(false, false, AutomaticOffer.Open)]
    [InlineData(true, false, AutomaticOffer.Postpone)]
    [InlineData(false, true, AutomaticOffer.Postpone)]
    [InlineData(true, true, AutomaticOffer.Postpone)]
    public void The_box_of_a_waiting_version_opens_only_when_FolderSwitch_is_not_in_use(bool busy, bool inUse, AutomaticOffer expected)
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.Hold(V011);

        Assert.Equal(expected, announcements.Decide(busy, inUse));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Without_a_waiting_version_there_is_nothing_to_open(bool busy, bool inUse)
    {
        var announcements = new UpdateAnnouncements<Version>();

        Assert.Equal(AutomaticOffer.None, announcements.Decide(busy, inUse));
    }

    [Fact]
    public void A_postponed_version_is_offered_once_FolderSwitch_is_left()
    {
        var announcements = new UpdateAnnouncements<Version>();
        announcements.Hold(V011);

        // In use at the check and at the first retries, then idle: the box opens, once.
        Assert.Equal(AutomaticOffer.Postpone, announcements.Decide(busy: false, inUse: true));
        Assert.Equal(AutomaticOffer.Postpone, announcements.Decide(busy: false, inUse: true));
        Assert.Equal(AutomaticOffer.Open, announcements.Decide(busy: false, inUse: false));
        Assert.True(announcements.TryAnnounce(V011));
        Assert.Equal(AutomaticOffer.None, announcements.Decide(busy: false, inUse: false));

        // The next periodic check finds the same version: it does not wait again.
        Assert.False(announcements.Hold(V011));
    }
}
