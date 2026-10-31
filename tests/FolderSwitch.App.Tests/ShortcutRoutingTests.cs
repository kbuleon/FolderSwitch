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
}
