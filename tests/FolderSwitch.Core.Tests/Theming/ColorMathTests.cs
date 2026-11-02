// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Theming;

namespace FolderSwitch.Core.Tests.Theming;

public class ColorMathTests
{
    [Fact]
    public void ContrastRatio_black_on_white_is_21()
    {
        Assert.Equal(21.0, ColorMath.ContrastRatio("#000000", "#ffffff"), 6);
        Assert.Equal(21.0, ColorMath.ContrastRatio("#ffffff", "#000000"), 6);
    }

    [Fact]
    public void ContrastRatio_same_color_is_1()
    {
        Assert.Equal(1.0, ColorMath.ContrastRatio("#3b82f6", "#3b82f6"), 6);
    }

    [Fact]
    public void ContrastRatio_ignores_alpha_and_case()
    {
        // Alpha comes first (WPF order) and does not take part in the contrast.
        Assert.Equal(21.0, ColorMath.ContrastRatio("#00000000", "#FFFFFF"), 6);
        Assert.Equal(21.0, ColorMath.ContrastRatio("#80000000", "#ffffffff"), 6);
        Assert.Equal(
            ColorMath.ContrastRatio("#1c1c22", "#f7f5f0"),
            ColorMath.ContrastRatio("#FF1C1C22", "#F7F5F0"),
            9);
    }

    [Fact]
    public void ContrastRatio_matches_a_known_wcag_value()
    {
        // #777777 on white is the classic "just under 4.5" gray: 4.48:1.
        Assert.Equal(4.48, ColorMath.ContrastRatio("#777777", "#ffffff"), 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("ffffff")]
    [InlineData("#fff")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#gggggg")]
    [InlineData(" #ffffff")]
    [InlineData("#ffffff ")]
    [InlineData("#+fffff")]
    [InlineData("#1234567890")]
    public void Invalid_colors_are_rejected(string bad)
    {
        Assert.Throws<FormatException>(() => ColorMath.ContrastRatio(bad, "#ffffff"));
        Assert.Throws<FormatException>(() => ColorMath.ContrastRatio("#ffffff", bad));
        Assert.Throws<FormatException>(() => ColorMath.Mix(bad, 0.5, "#ffffff"));
        Assert.Throws<FormatException>(() => ColorMath.Mix("#ffffff", 0.5, bad));
        Assert.Throws<FormatException>(() => ColorMath.EnsureContrast(bad, "#ffffff", 4.5));
        Assert.Throws<FormatException>(() => ColorMath.EnsureContrast("#ffffff", bad, 4.5));
        Assert.False(ColorMath.IsValidHex(bad));
    }

    [Fact]
    public void Null_colors_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ColorMath.ContrastRatio(null!, "#ffffff"));
        Assert.Throws<ArgumentNullException>(() => ColorMath.Mix("#ffffff", 0.5, null!));
        Assert.False(ColorMath.IsValidHex(null));
    }

    [Theory]
    [InlineData("#ffffff")]
    [InlineData("#FFFFFF")]
    [InlineData("#80ff00aa")]
    [InlineData("#3B82f6")]
    public void IsValidHex_accepts_rgb_and_argb_in_any_case(string good)
    {
        Assert.True(ColorMath.IsValidHex(good));
    }

    [Theory]
    // Mix(color, weight, with) = color * weight + with * (1 - weight), rounded per channel.
    [InlineData("#000000", 0.5, "#ffffff", "#808080")]
    [InlineData("#ff0000", 0.25, "#0000ff", "#4000bf")]
    [InlineData("#3b82f6", 0.11, "#ffffff", "#e9f1fe")]
    [InlineData("#3b82f6", 1.0, "#ffffff", "#3b82f6")]
    [InlineData("#3B82F6", 0.0, "#FFFFFF", "#ffffff")]
    // Alpha is ignored; output is always lowercase #rrggbb.
    [InlineData("#80FF0000", 0.5, "#FF000000", "#800000")]
    public void Mix_blends_per_channel(string color, double weight, string with, string expected)
    {
        Assert.Equal(expected, ColorMath.Mix(color, weight, with));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Mix_rejects_a_weight_outside_zero_one(double weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorMath.Mix("#000000", weight, "#ffffff"));
    }

    [Fact]
    public void EnsureContrast_returns_the_normalized_foreground_when_already_readable()
    {
        Assert.Equal("#1c1c22", ColorMath.EnsureContrast("#1c1c22", "#ffffff", 4.5));
        Assert.Equal("#1c1c22", ColorMath.EnsureContrast("#FF1C1C22", "#ffffff", 4.5));
    }

    [Fact]
    public void EnsureContrast_darkens_a_gray_on_white_with_the_smallest_step()
    {
        // 5 % toward black is enough: #717171 on white is about 4.9:1.
        Assert.Equal("#717171", ColorMath.EnsureContrast("#777777", "#ffffff", 4.5));
    }

    [Fact]
    public void EnsureContrast_lightens_a_dark_color_on_a_dark_background()
    {
        var result = ColorMath.EnsureContrast("#333333", "#000000", 4.5);

        Assert.True(ColorMath.ContrastRatio(result, "#000000") >= 4.5);
        Assert.NotEqual("#ffffff", result);
        Assert.True(Convert.ToInt32(result.Substring(1, 2), 16) > 0x33);
    }

    [Fact]
    public void EnsureContrast_white_on_amber_reaches_the_ratio()
    {
        var result = ColorMath.EnsureContrast("#ffffff", "#f59e0b", 4.5);

        Assert.True(ColorMath.ContrastRatio(result, "#f59e0b") >= 4.5);
    }

    [Fact]
    public void EnsureContrast_always_reaches_4_5_for_any_background()
    {
        int[] levels = [0, 51, 102, 153, 204, 255];
        string[] foregrounds = ["#ffffff", "#000000", "#777777", "#3b82f6", "#f59e0b"];

        foreach (var r in levels)
        {
            foreach (var g in levels)
            {
                foreach (var b in levels)
                {
                    var background = $"#{r:x2}{g:x2}{b:x2}";
                    foreach (var foreground in foregrounds)
                    {
                        var result = ColorMath.EnsureContrast(foreground, background, 4.5);
                        Assert.True(
                            ColorMath.ContrastRatio(result, background) >= 4.5,
                            $"{foreground} on {background} -> {result}");
                    }
                }
            }
        }
    }

    [Fact]
    public void EnsureContrast_falls_back_to_the_better_of_black_and_white_when_unreachable()
    {
        // Mid gray: black gives about 5.3:1, white about 3.9:1; 30:1 is out of reach for both.
        Assert.Equal("#000000", ColorMath.EnsureContrast("#808080", "#808080", 30));
        Assert.Equal("#ffffff", ColorMath.EnsureContrast("#808080", "#202020", 30));
    }

    [Fact]
    public void EnsureBackgroundContrast_returns_the_normalized_background_when_already_readable()
    {
        Assert.Equal("#1c1c22", ColorMath.EnsureBackgroundContrast("#ffffff", "#1c1c22", 4.5));
        Assert.Equal("#1c1c22", ColorMath.EnsureBackgroundContrast("#FFFFFFFF", "#FF1C1C22", 4.5));
    }

    [Fact]
    public void EnsureBackgroundContrast_darkens_a_background_for_a_white_foreground_with_the_smallest_step()
    {
        // #777777 under white is 4.48:1; 5 % toward black (#717171) is enough.
        Assert.Equal("#717171", ColorMath.EnsureBackgroundContrast("#ffffff", "#777777", 4.5));
    }

    [Fact]
    public void EnsureBackgroundContrast_lightens_a_background_for_a_dark_foreground()
    {
        var result = ColorMath.EnsureBackgroundContrast("#000000", "#555555", 4.5);

        Assert.True(ColorMath.ContrastRatio("#000000", result) >= 4.5);
        Assert.True(Convert.ToInt32(result.Substring(1, 2), 16) > 0x55);
    }

    [Fact]
    public void EnsureBackgroundContrast_keeps_the_hue_of_the_background_when_darkening()
    {
        // Amber under white is 2.15:1: the result is a blend of amber with black.
        var result = ColorMath.EnsureBackgroundContrast("#ffffff", "#f59e0b", 4.5);

        Assert.True(ColorMath.ContrastRatio("#ffffff", result) >= 4.5);
        Assert.Contains(
            result,
            Enumerable.Range(1, 20).Select(step => ColorMath.Mix("#000000", step / 20.0, "#f59e0b")));
    }

    [Fact]
    public void EnsureBackgroundContrast_always_reaches_4_5_for_any_foreground()
    {
        int[] levels = [0, 51, 102, 153, 204, 255];
        string[] backgrounds = ["#ffffff", "#000000", "#777777", "#3b82f6", "#f59e0b"];

        foreach (var r in levels)
        {
            foreach (var g in levels)
            {
                foreach (var b in levels)
                {
                    var foreground = $"#{r:x2}{g:x2}{b:x2}";
                    foreach (var background in backgrounds)
                    {
                        var result = ColorMath.EnsureBackgroundContrast(foreground, background, 4.5);
                        Assert.True(
                            ColorMath.ContrastRatio(foreground, result) >= 4.5,
                            $"{foreground} on {background} -> {result}");
                    }
                }
            }
        }
    }

    [Fact]
    public void EnsureBackgroundContrast_falls_back_to_the_better_of_black_and_white_when_unreachable()
    {
        Assert.Equal("#000000", ColorMath.EnsureBackgroundContrast("#808080", "#808080", 30));
        Assert.Equal("#ffffff", ColorMath.EnsureBackgroundContrast("#202020", "#808080", 30));
    }

    [Fact]
    public void EnsureBackgroundContrast_rejects_bad_colors()
    {
        Assert.Throws<FormatException>(() => ColorMath.EnsureBackgroundContrast("red", "#ffffff", 4.5));
        Assert.Throws<FormatException>(() => ColorMath.EnsureBackgroundContrast("#ffffff", "#fff", 4.5));
        Assert.Throws<ArgumentNullException>(() => ColorMath.EnsureBackgroundContrast(null!, "#ffffff", 4.5));
    }
}
