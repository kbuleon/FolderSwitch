// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>The small icon Windows associates with a file extension, read without touching any file.</summary>
public sealed class FileTypeIconsTests
{
    [Fact]
    public void Known_extension_gives_pixels()
    {
        using var icons = new FileTypeIcons();

        var pixels = icons.SmallIconFor(".txt");

        Assert.NotNull(pixels);
        Assert.True(pixels.Width > 0);
        Assert.True(pixels.Height > 0);
        Assert.Equal(pixels.Width * pixels.Height * 4, pixels.Bgra32.Length);
        Assert.Contains(pixels.Bgra32.Where((_, i) => i % 4 == 3), alpha => alpha != 0);
    }

    [Fact]
    public void Unknown_extension_gives_generic_or_null_without_throwing()
    {
        using var icons = new FileTypeIcons();

        var pixels = icons.SmallIconFor(".zzzqqq");

        if (pixels is not null)
        {
            Assert.True(pixels.Width > 0);
            Assert.Equal(pixels.Width * pixels.Height * 4, pixels.Bgra32.Length);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("txt")]
    [InlineData(".")]
    [InlineData("..\\x")]
    public void An_odd_extension_never_throws(string extension)
    {
        using var icons = new FileTypeIcons();

        _ = icons.SmallIconFor(extension);
    }

    [Fact]
    public void The_extension_is_cached_in_lower_case()
    {
        using var icons = new FileTypeIcons();

        var upper = icons.SmallIconFor(".TXT");
        var lower = icons.SmallIconFor(".txt");

        Assert.Same(upper, lower);
    }

    [Fact]
    public async Task Concurrent_calls_agree()
    {
        using var icons = new FileTypeIcons();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => icons.SmallIconFor(".docx"))));

        Assert.All(results, r => Assert.NotNull(r));
        Assert.All(results, r => Assert.Same(results[0], r));
    }
}
