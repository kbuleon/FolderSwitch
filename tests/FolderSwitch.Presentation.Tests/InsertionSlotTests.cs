using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public class InsertionSlotTests
{
    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(3, 2, 2)]
    [InlineData(4, 2, 3)]
    [InlineData(5, 2, 4)]
    public void ToMoveIndex_within_the_same_list_accounts_for_the_element_taken_out(int slot, int source, int expected) =>
        Assert.Equal(expected, InsertionSlot.ToMoveIndex(slot, source));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    public void ToMoveIndex_from_another_list_is_the_slot(int slot, int expected) =>
        Assert.Equal(expected, InsertionSlot.ToMoveIndex(slot, -1));

    [Fact]
    public void ToMoveIndex_never_goes_below_zero() => Assert.Equal(0, InsertionSlot.ToMoveIndex(-3, -1));

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(3, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(4, 2, false)]
    [InlineData(0, -1, false)]
    public void IsNoMove_only_for_the_gaps_around_the_element_itself(int slot, int source, bool expected) =>
        Assert.Equal(expected, InsertionSlot.IsNoMove(slot, source));

    [Fact]
    public void Dropping_a_tab_after_the_next_one_moves_it_one_place_right()
    {
        var h = new Harness();
        var vm = h.Customizing();

        // Work, Home, Misc: « Work » dropped in the gap between « Home » and « Misc » (slot 2).
        vm.MoveTab(h.TabVm("Work"), InsertionSlot.ToMoveIndex(2, 0));

        Assert.Equal(["Home", "Work", "Misc"], vm.Tabs.Select(t => t.Name));
    }

    [Fact]
    public void Dropping_a_tab_after_the_last_one_moves_it_to_the_end()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.MoveTab(h.TabVm("Work"), InsertionSlot.ToMoveIndex(3, 0));

        Assert.Equal(["Home", "Misc", "Work"], vm.Tabs.Select(t => t.Name));
    }

    [Fact]
    public void Dropping_an_item_in_another_group_inserts_it_at_the_slot()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var media = h.GroupVm("Media");

        // Media holds Music and Notes.txt: Acme dropped between them (slot 1).
        vm.MoveItem(h.ItemVm("Acme"), media, InsertionSlot.ToMoveIndex(1, -1));

        Assert.Equal(["Music", "Acme", "Notes.txt"], media.Items.Select(i => i.DisplayName));
    }
}
