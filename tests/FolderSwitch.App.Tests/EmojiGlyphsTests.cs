// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.App.Views;

namespace FolderSwitch.App.Tests;

/// <summary>
/// What the machine's emoji font (Segoe UI Emoji, present on every Windows 10 and 11) can draw, read from its glyph
/// table.
/// </summary>
public sealed class EmojiGlyphsTests
{
    private readonly EmojiGlyphs _glyphs = new();

    [Theory]
    [InlineData("💵")]
    [InlineData("❤️")]
    [InlineData("❤")]
    public void An_emoji_of_the_font_can_be_drawn(string emoji) => Assert.True(_glyphs.CanDraw(emoji));

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("\uFE0F")]
    public void What_the_font_has_no_glyph_for_cannot_be_drawn(string text) => Assert.False(_glyphs.CanDraw(text));

    [Fact]
    public void A_lone_surrogate_cannot_be_drawn() => Assert.False(_glyphs.CanDraw("\uD83D"));

    [Fact]
    public void It_answers_from_several_threads_at_once()
    {
        var answers = new bool[64];
        Parallel.For(0, answers.Length, index => answers[index] = _glyphs.CanDraw(index % 2 == 0 ? "💵" : ""));

        Assert.All(answers.Select((answer, index) => (answer, index)), pair => Assert.Equal(pair.index % 2 == 0, pair.answer));
    }
}
