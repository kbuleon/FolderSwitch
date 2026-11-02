// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Icons;

namespace FolderSwitch.Core.Tests.Icons;

public class IconTextTests
{
    [Theory]
    [InlineData("\U0001F4B5")] // 💵
    [InlineData("❤\uFE0F")] // ❤ with the emoji variation selector
    [InlineData("✂")] // ✂
    public void Plausible_icons(string icon)
    {
        Assert.True(IconText.IsPlausible(icon));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("1")]
    [InlineData("\U0001F4B5\U0001F4B5")]
    [InlineData(" ")]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467")] // a ZWJ family sequence
    public void Implausible_icons(string? icon)
    {
        Assert.False(IconText.IsPlausible(icon));
    }
}
