// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.App.Tests;

/// <summary>Which update feed addresses FolderSwitch reads: a GitHub repository, nothing else.</summary>
public class UpdateFeedTests
{
    [Theory]
    [InlineData("https://github.com/kbuleon/FolderSwitch")]
    [InlineData("https://github.com/kbuleon/FolderSwitch/")]
    [InlineData("HTTPS://GITHUB.COM/kbuleon/FolderSwitch")]
    [InlineData("  https://github.com/kbuleon/FolderSwitch  ")]
    public void GitHub_repository_addresses_are_accepted(string url)
    {
        Assert.True(UpdateFeed.TryParseRepository(url, out var repository, out var name));
        Assert.Equal("kbuleon/FolderSwitch", name);
        Assert.Equal("https://github.com/kbuleon/FolderSwitch", repository.ToString());
    }

    [Theory]
    [InlineData("http://github.com/kbuleon/FolderSwitch")]
    [InlineData("https://www.github.com/kbuleon/FolderSwitch")]
    [InlineData("https://api.github.com/repos/kbuleon/FolderSwitch")]
    [InlineData("https://github.com.example.org/kbuleon/FolderSwitch")]
    [InlineData("https://github.com/kbuleon")]
    [InlineData("https://github.com/kbuleon/FolderSwitch/releases")]
    [InlineData("https://github.com/kbuleon/FolderSwitch.git")]
    [InlineData("https://github.com/kbuleon/FolderSwitch?tab=readme")]
    [InlineData("https://github.com/kbuleon/FolderSwitch#readme")]
    [InlineData("https://example.org/folderswitch/")]
    [InlineData("http://localhost:8000/")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Any_other_address_is_refused(string? url)
    {
        Assert.False(UpdateFeed.TryParseRepository(url, out _, out _));
    }

    [Fact]
    public void The_source_reads_stable_releases_of_the_repository()
    {
        var source = UpdateFeed.CreateSource("https://github.com/kbuleon/FolderSwitch/");

        Assert.NotNull(source);
        Assert.Equal(new Uri("https://github.com/kbuleon/FolderSwitch"), source.RepoUri);
        Assert.False(source.Prerelease);
    }

    [Fact]
    public void No_source_for_another_address()
    {
        Assert.Null(UpdateFeed.CreateSource("https://example.org/folderswitch/"));
    }

    [Fact]
    public void The_shipped_address_is_the_repository()
    {
        Assert.Equal("https://github.com/kbuleon/FolderSwitch", UpdateFeed.Url);
        Assert.True(UpdateFeed.TryParseRepository(UpdateFeed.Url, out _, out _));
    }
}
