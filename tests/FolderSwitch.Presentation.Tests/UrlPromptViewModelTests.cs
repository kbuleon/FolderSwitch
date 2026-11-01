using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public class UrlPromptViewModelTests
{
    [Fact]
    public void A_web_address_on_the_clipboard_prefills_the_field()
    {
        var vm = new UrlPromptViewModel("https://a.fr/x");

        Assert.Equal("https://a.fr/x", vm.Text);
        Assert.True(vm.CanAdd);
        Assert.False(vm.ShowsError);
    }

    [Theory]
    [InlineData("bonjour")]
    [InlineData("a.fr")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_on_the_clipboard_leaves_the_field_empty(string? clipboard)
    {
        var vm = new UrlPromptViewModel(clipboard);

        Assert.Equal(string.Empty, vm.Text);
        Assert.False(vm.CanAdd);
        Assert.False(vm.ShowsError);
    }

    [Fact]
    public void An_address_without_a_scheme_is_completed_by_Accept()
    {
        var vm = new UrlPromptViewModel(null) { Text = "a.fr" };

        Assert.True(vm.CanAdd);
        Assert.Null(vm.Result);

        vm.Accept();

        Assert.Equal("https://a.fr/", vm.Result);
    }

    [Fact]
    public void Another_scheme_cannot_be_added_and_shows_the_error()
    {
        var vm = new UrlPromptViewModel(null) { Text = "javascript:x" };

        Assert.False(vm.CanAdd);
        Assert.True(vm.ShowsError);

        vm.Accept();

        Assert.Null(vm.Result);
    }

    [Fact]
    public void An_empty_field_cannot_be_added_and_shows_no_error()
    {
        var vm = new UrlPromptViewModel(null) { Text = "" };

        Assert.False(vm.CanAdd);
        Assert.False(vm.ShowsError);
    }

    [Fact]
    public void Editing_the_text_updates_CanAdd_and_ShowsError()
    {
        var vm = new UrlPromptViewModel(null);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Text = "ftp://x";

        Assert.Contains(nameof(UrlPromptViewModel.CanAdd), changed);
        Assert.Contains(nameof(UrlPromptViewModel.ShowsError), changed);
    }
}
