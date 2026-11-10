// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

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

    [Theory]
    [InlineData("file:///C:/wiki/index.html")]
    [InlineData("ftp://files.a.fr/pub")]
    public void A_file_or_ftp_address_prefills_the_field_and_is_accepted(string address)
    {
        var vm = new UrlPromptViewModel(address);

        Assert.Equal(address, vm.Text);
        Assert.True(vm.CanAdd);

        vm.Accept();

        Assert.Equal(address, vm.Result);
    }

    [Theory]
    [InlineData(@"C:\wiki\index.html")]
    [InlineData("ftp://user:pass@a.fr/")]
    [InlineData("file://user:pass@server/share/x.html")]
    public void A_path_or_an_address_with_credentials_cannot_be_added(string text)
    {
        var vm = new UrlPromptViewModel(null) { Text = text };

        Assert.False(vm.CanAdd);
        Assert.True(vm.ShowsError);
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

        vm.Text = "gopher://x";

        Assert.Contains(nameof(UrlPromptViewModel.CanAdd), changed);
        Assert.Contains(nameof(UrlPromptViewModel.ShowsError), changed);
    }
}
