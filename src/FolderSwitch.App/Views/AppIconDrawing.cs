using System.Windows;
using System.Windows.Media;

namespace FolderSwitch.App.Views;

/// <summary>
/// The FolderSwitch icon as a drawing on its 32 × 32 grid (spec 5.10): the tall folder, in a diagonal gradient from cyan
/// (<c>#22d3ee</c>) to blue (<c>#2563eb</c>), struck by a white bolt that casts a shadow (<c>#0f2a6b</c> at 45 %,
/// shifted by one unit).
/// </summary>
/// <remarks>
/// The one definition of the icon in code, used twice: the title bars show <see cref="Glyph"/> (a vector image, crisp at
/// any size and scale), and the icon builder (<c>tools/FolderSwitch.IconBuilder</c>, which compiles this very file)
/// renders <see cref="Draw"/> into <c>assets/icon/FolderSwitch.ico</c> and its PNG files, so that the file icon (the
/// executable, the notification area, the taskbar) and the title bars cannot drift apart. <c>assets/icon/FolderSwitch.svg</c>
/// is the same geometry as a master file for other tools: it is edited together with this class.
/// </remarks>
internal static class AppIconDrawing
{
    /// <summary>The side of the grid the icon is drawn on.</summary>
    public const double Grid = 32;

    private const string FolderPath = "M2 6.5a3 3 0 0 1 3-3h7l3 3h12a3 3 0 0 1 3 3v17a3 3 0 0 1-3 3H5a3 3 0 0 1-3-3z";
    private const string BoltPath = "M18.6 8.6 9.85 19.25h6.5l-2 8.15 9-11.3h-6.65z";

    private static DrawingImage? _glyph;

    /// <summary>The icon as a vector image on its 32 × 32 grid, frozen: for the title bars, at any size and scale.</summary>
    public static DrawingImage Glyph => _glyph ??= CreateGlyph();

    /// <summary>Draws the icon on its 32 × 32 grid: the folder, then the bolt over its own shadow.</summary>
    public static void Draw(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var folder = Geometry.Parse(FolderPath);
        var bolt = Geometry.Parse(BoltPath);

        // Relative to the folder's bounding box: from its top left corner to its bottom right corner.
        var gradient = new LinearGradientBrush(
            Color.FromRgb(0x22, 0xd3, 0xee),
            Color.FromRgb(0x25, 0x63, 0xeb),
            new Point(0, 0),
            new Point(1, 1));
        var shadow = new SolidColorBrush(Color.FromRgb(0x0f, 0x2a, 0x6b)) { Opacity = 0.45 };

        context.DrawGeometry(gradient, null, folder);
        context.PushTransform(new TranslateTransform(1, 1));
        context.DrawGeometry(shadow, null, bolt);
        context.Pop();
        context.DrawGeometry(Brushes.White, null, bolt);
    }

    private static DrawingImage CreateGlyph()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            // The whole grid, so that the image keeps the icon's margins.
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Grid, Grid));
            Draw(context);
        }

        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
