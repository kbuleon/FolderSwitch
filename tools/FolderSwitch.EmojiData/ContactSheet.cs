// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderSwitch.EmojiData;

/// <summary>The font the application draws icons with, and what this machine's copy of it can draw.</summary>
internal static class EmojiFont
{
    public const string FamilyName = "Segoe UI Emoji";

    public static Typeface Typeface { get; } = new(new FontFamily(FamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    /// <summary>The font's glyphs by code point; throws when the machine does not have the font.</summary>
    public static IDictionary<int, ushort> GlyphMap
    {
        get
        {
            if (!Typeface.TryGetGlyphTypeface(out var glyphTypeface) || !glyphTypeface.FamilyNames.Values.Contains(FamilyName))
            {
                throw new InvalidOperationException($"The font '{FamilyName}' is not installed on this machine.");
            }

            return glyphTypeface.CharacterToGlyphMap;
        }
    }

    /// <summary>The emoji as the application draws it: one colour, the colour of the text. Must be called on an STA thread.</summary>
    public static FormattedText Format(string emoji, double size, Brush brush) =>
        new(emoji, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, size, brush, 1.0);

    /// <summary>Whether WPF draws something for the emoji (an emoji the font lacks can come out as an empty outline).</summary>
    public static bool HasOutline(string emoji)
    {
        var geometry = Format(emoji, 16, Brushes.Black).BuildGeometry(new Point(0, 0));
        return geometry is not null && !geometry.IsEmpty() && !geometry.Bounds.IsEmpty && geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0;
    }
}

/// <summary>
/// Contact sheets: PNG files of the catalog as the application draws it (WPF, one dark grey on a light ground, at twice
/// the usual DPI), each emoji at the size of an icon with its code point under it, so that the ones that are unreadable
/// at 16 px can be spotted and listed in <c>excluded.txt</c>.
/// </summary>
internal static class ContactSheet
{
    private const int Columns = 14;
    private const int RowsPerSheet = 11;
    private const double CellWidth = 56;
    private const double CellHeight = 50;
    private const double Margin = 8;
    private const double Dpi = 192;

    /// <summary>Writes <c>&lt;category&gt;-&lt;page&gt;.png</c> files into <paramref name="directory"/>; returns their paths. Must be called on an STA thread.</summary>
    public static IReadOnlyList<string> Write(string directory, IReadOnlyList<(string Category, string Emoji)> catalog)
    {
        var written = new List<string>();
        var ink = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        var caption = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
        var ground = new SolidColorBrush(Color.FromRgb(0xF6, 0xF6, 0xF4));

        foreach (var (category, _) in EmojiTestFile.Categories)
        {
            var emojis = catalog.Where(entry => entry.Category == category).Select(entry => entry.Emoji).ToList();
            var perSheet = Columns * RowsPerSheet;
            var pages = (emojis.Count + perSheet - 1) / perSheet;

            for (var page = 0; page < pages; page++)
            {
                var chunk = emojis.Skip(page * perSheet).Take(perSheet).ToList();
                var rows = (chunk.Count + Columns - 1) / Columns;
                var width = Margin * 2 + Columns * CellWidth;
                var height = Margin * 2 + rows * CellHeight;

                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawRectangle(ground, null, new Rect(0, 0, width, height));
                    for (var index = 0; index < chunk.Count; index++)
                    {
                        var x = Margin + (index % Columns) * CellWidth;
                        var y = Margin + (index / Columns) * CellHeight;

                        var glyph = EmojiFont.Format(chunk[index], 16, ink);
                        context.DrawText(glyph, new Point(x + (CellWidth - glyph.WidthIncludingTrailingWhitespace) / 2, y + 4));

                        var code = new FormattedText(
                            CodePoint(chunk[index]), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 7, caption, 1.0);
                        context.DrawText(code, new Point(x + (CellWidth - code.WidthIncludingTrailingWhitespace) / 2, y + 30));
                    }
                }

                var bitmap = new RenderTargetBitmap((int)(width * Dpi / 96), (int)(height * Dpi / 96), Dpi, Dpi, PixelFormats.Pbgra32);
                bitmap.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var path = Path.Combine(directory, $"{category}-{page + 1}.png");
                using (var stream = File.Create(path))
                {
                    encoder.Save(stream);
                }

                written.Add(path);
            }
        }

        return written;
    }

    private static string CodePoint(string emoji) =>
        string.Join(' ', EmojiTestFile.BaseOf(emoji).EnumerateRunes().Select(rune => rune.Value.ToString("X4", CultureInfo.InvariantCulture)));
}
