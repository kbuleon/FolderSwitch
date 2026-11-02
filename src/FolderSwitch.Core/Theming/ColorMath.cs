// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;

namespace FolderSwitch.Core.Theming;

/// <summary>
/// Color arithmetic on <c>#RRGGBB</c> / <c>#AARRGGBB</c> strings (WPF order, alpha first, any case).
/// Alpha is ignored by every operation; results are lowercase <c>#rrggbb</c>.
/// </summary>
public static class ColorMath
{
    private const int StepCount = 20;

    private static readonly Rgb PureBlack = new(0, 0, 0);
    private static readonly Rgb PureWhite = new(255, 255, 255);

    /// <summary>True when <paramref name="value"/> is a well-formed <c>#RRGGBB</c> or <c>#AARRGGBB</c> color.</summary>
    public static bool IsValidHex(string? value) => TryParse(value, out _);

    /// <summary>WCAG 2.x contrast ratio between two colors, from 1 (identical) to 21 (black on white).</summary>
    /// <exception cref="FormatException">A color is not <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</exception>
    public static double ContrastRatio(string a, string b) => Ratio(Parse(a), Parse(b));

    /// <summary>
    /// <c>color * weight + with * (1 - weight)</c> per channel, rounded to the nearest integer
    /// (halves away from zero). <paramref name="weight"/> is the share of <paramref name="color"/>, in 0..1.
    /// </summary>
    /// <exception cref="FormatException">A color is not <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is outside 0..1.</exception>
    public static string Mix(string color, double weight, string with)
    {
        var mixed = MixRgb(Parse(color), weight, Parse(with));
        return Format(mixed);
    }

    /// <summary>
    /// Returns <paramref name="foreground"/> (normalized) when it already reaches
    /// <paramref name="minRatio"/> against <paramref name="background"/>. Otherwise blends it toward
    /// black and toward white in 5 % steps and returns the first blend that reaches the ratio
    /// (smallest step, black side first on ties). Pure black or white always reaches 4.5:1 against
    /// any color; if the requested ratio is out of reach for both, the better of the two is returned.
    /// </summary>
    /// <exception cref="FormatException">A color is not <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</exception>
    public static string EnsureContrast(string foreground, string background, double minRatio) =>
        Format(Adjust(Parse(foreground), Parse(background), minRatio));

    /// <summary>
    /// Mirror of <see cref="EnsureContrast"/> that moves the <em>background</em> instead of the
    /// foreground: returns <paramref name="background"/> (normalized) when it already reaches
    /// <paramref name="minRatio"/> against <paramref name="foreground"/>, otherwise the first blend of
    /// it with black or white (5 % steps, smallest step, black side first on ties) that does. A
    /// background keeps its hue while it is darkened or lightened; if the requested ratio is out of
    /// reach for every blend, the better of pure black or white is returned.
    /// </summary>
    /// <exception cref="FormatException">A color is not <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</exception>
    public static string EnsureBackgroundContrast(string foreground, string background, double minRatio) =>
        Format(Adjust(Parse(background), Parse(foreground), minRatio));

    /// <summary>
    /// Moves <paramref name="start"/> away from <paramref name="other"/> until their contrast reaches
    /// <paramref name="minRatio"/>. The contrast ratio is symmetric, so the same search serves a
    /// foreground moved over a fixed background and a background moved under a fixed foreground.
    /// </summary>
    private static Rgb Adjust(Rgb start, Rgb other, double minRatio)
    {
        if (Ratio(start, other) >= minRatio)
        {
            return start;
        }

        for (var step = 1; step <= StepCount; step++)
        {
            var weight = step / (double)StepCount;

            var towardBlack = MixRgb(PureBlack, weight, start);
            if (Ratio(towardBlack, other) >= minRatio)
            {
                return towardBlack;
            }

            var towardWhite = MixRgb(PureWhite, weight, start);
            if (Ratio(towardWhite, other) >= minRatio)
            {
                return towardWhite;
            }
        }

        return Ratio(PureBlack, other) >= Ratio(PureWhite, other) ? PureBlack : PureWhite;
    }

    /// <summary>Lowercase <c>#rrggbb</c> form of a valid color; the alpha, if any, is dropped.</summary>
    internal static string Normalize(string value) => Format(Parse(value));

    private static Rgb Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return TryParse(value, out var rgb)
            ? rgb
            : throw new FormatException($"'{value}' is not a color; expected #RRGGBB or #AARRGGBB.");
    }

    private static bool TryParse(string? value, out Rgb rgb)
    {
        rgb = default;

        if (value is null || value.Length is not (7 or 9) || value[0] != '#')
        {
            return false;
        }

        // With an alpha channel the color bytes start after the two alpha digits.
        var start = value.Length == 9 ? 3 : 1;

        if (value.Length == 9 && !TryHexByte(value, 1, out _))
        {
            return false;
        }

        if (TryHexByte(value, start, out var r)
            && TryHexByte(value, start + 2, out var g)
            && TryHexByte(value, start + 4, out var b))
        {
            rgb = new Rgb(r, g, b);
            return true;
        }

        return false;
    }

    private static bool TryHexByte(string value, int index, out byte result) =>
        byte.TryParse(value.AsSpan(index, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);

    private static Rgb MixRgb(Rgb color, double weight, Rgb with)
    {
        if (!(weight >= 0.0 && weight <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "The weight must be between 0 and 1.");
        }

        return new Rgb(
            MixChannel(color.R, weight, with.R),
            MixChannel(color.G, weight, with.G),
            MixChannel(color.B, weight, with.B));
    }

    private static byte MixChannel(byte color, double weight, byte with) =>
        (byte)Math.Round(color * weight + with * (1.0 - weight), MidpointRounding.AwayFromZero);

    private static string Format(Rgb c) =>
        string.Create(CultureInfo.InvariantCulture, $"#{c.R:x2}{c.G:x2}{c.B:x2}");

    private static double Ratio(Rgb a, Rgb b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Rgb c) =>
        0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    private static double Linear(byte channel)
    {
        var s = channel / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private readonly record struct Rgb(byte R, byte G, byte B);
}
