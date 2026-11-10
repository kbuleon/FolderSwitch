// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.App.Startup;

namespace FolderSwitch.App.Tests;

/// <summary>What a press of the global shortcut does, from what is on screen.</summary>
public class ShortcutRoutingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void A_hidden_window_is_shown(bool ownWindowInFront, bool promptOpen)
    {
        // A prompt cannot be open with the window hidden in practice; the window is shown regardless.
        Assert.Equal(ShortcutAction.Show, ShortcutRouting.Decide(false, ownWindowInFront, hotkeyTestShown: false, promptOpen));
    }

    [Fact]
    public void A_window_in_front_hides()
    {
        Assert.Equal(ShortcutAction.Hide, ShortcutRouting.Decide(true, ownWindowInFront: true, hotkeyTestShown: false, promptOpen: false));
    }

    [Fact]
    public void A_window_behind_another_one_is_recalled_not_hidden()
    {
        // Customize mode, the user went to Explorer to browse to a folder, then pressed the shortcut to come back.
        Assert.Equal(ShortcutAction.Recall, ShortcutRouting.Decide(true, ownWindowInFront: false, hotkeyTestShown: false, promptOpen: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_open_box_of_FolderSwitch_must_be_answered_first(bool ownWindowInFront)
    {
        Assert.Equal(ShortcutAction.None, ShortcutRouting.Decide(true, ownWindowInFront, hotkeyTestShown: false, promptOpen: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void With_the_shortcut_page_in_front_the_press_goes_to_its_test_zone(bool promptOpen)
    {
        Assert.Equal(ShortcutAction.TestZone, ShortcutRouting.Decide(true, ownWindowInFront: true, hotkeyTestShown: true, promptOpen));
    }

    [Fact]
    public void A_window_behind_another_one_is_recalled_even_on_the_shortcut_page()
    {
        // The settings stay open when the user goes to Explorer: the press brings the window back, it does not test.
        Assert.Equal(ShortcutAction.Recall, ShortcutRouting.Decide(true, ownWindowInFront: false, hotkeyTestShown: true, promptOpen: false));
    }

    [Fact]
    public void The_shortcut_page_of_a_hidden_window_does_not_count()
    {
        Assert.Equal(ShortcutAction.Show, ShortcutRouting.Decide(false, ownWindowInFront: false, hotkeyTestShown: true, promptOpen: false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_box_open_with_the_window_hidden_comes_to_the_front_instead_of_the_window(bool ownWindowInFront)
    {
        // The update offer, opened by itself without the focus, or a message at startup: the press gives it the keyboard.
        Assert.Equal(ShortcutAction.FrontBox,
            ShortcutRouting.Decide(false, ownWindowInFront, hotkeyTestShown: false, promptOpen: true, boxOpen: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_box_open_over_the_window_comes_to_the_front_rather_than_hiding_or_recalling(bool ownWindowInFront)
    {
        // In front, or left behind another application with the window it belongs to.
        Assert.Equal(ShortcutAction.FrontBox,
            ShortcutRouting.Decide(true, ownWindowInFront, hotkeyTestShown: false, promptOpen: true, boxOpen: true));
    }

    [Fact]
    public void A_box_open_over_the_shortcut_page_behind_another_window_comes_to_the_front()
    {
        Assert.Equal(ShortcutAction.FrontBox,
            ShortcutRouting.Decide(true, ownWindowInFront: false, hotkeyTestShown: true, promptOpen: true, boxOpen: true));
    }

    [Fact]
    public void The_shortcut_page_in_front_keeps_the_press_for_its_test_zone_even_with_a_box_open()
    {
        Assert.Equal(ShortcutAction.TestZone,
            ShortcutRouting.Decide(true, ownWindowInFront: true, hotkeyTestShown: true, promptOpen: true, boxOpen: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_open_Windows_picker_is_not_brought_forward(bool ownWindowInFront)
    {
        // A picker is not one of FolderSwitch's boxes: the press does nothing until it is answered.
        Assert.Equal(ShortcutAction.None,
            ShortcutRouting.Decide(true, ownWindowInFront, hotkeyTestShown: false, promptOpen: true, boxOpen: false));
    }
}
