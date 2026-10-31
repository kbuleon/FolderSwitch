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
        Assert.Equal(ShortcutAction.Show, ShortcutRouting.Decide(false, ownWindowInFront, settingsPanelOpen: false, promptOpen));
    }

    [Fact]
    public void A_window_in_front_hides()
    {
        Assert.Equal(ShortcutAction.Hide, ShortcutRouting.Decide(true, ownWindowInFront: true, settingsPanelOpen: false, promptOpen: false));
    }

    [Fact]
    public void A_window_behind_another_one_is_recalled_not_hidden()
    {
        // Customize mode, the user went to Explorer to browse to a folder, then pressed the shortcut to come back.
        Assert.Equal(ShortcutAction.Recall, ShortcutRouting.Decide(true, ownWindowInFront: false, settingsPanelOpen: false, promptOpen: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_open_box_of_FolderSwitch_must_be_answered_first(bool ownWindowInFront)
    {
        Assert.Equal(ShortcutAction.None, ShortcutRouting.Decide(true, ownWindowInFront, settingsPanelOpen: false, promptOpen: true));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void With_the_settings_panel_open_the_press_goes_to_its_test_zone(bool ownWindowInFront, bool promptOpen)
    {
        Assert.Equal(ShortcutAction.TestZone, ShortcutRouting.Decide(true, ownWindowInFront, settingsPanelOpen: true, promptOpen));
    }

    [Fact]
    public void The_settings_panel_of_a_hidden_window_does_not_count()
    {
        Assert.Equal(ShortcutAction.Show, ShortcutRouting.Decide(false, ownWindowInFront: false, settingsPanelOpen: true, promptOpen: false));
    }
}
