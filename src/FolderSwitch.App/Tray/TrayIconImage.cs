using System.IO;
using System.Windows;
using System.Windows.Media;
using FolderSwitch.App.Diagnostics;
using DrawingIcon = System.Drawing.Icon;

namespace FolderSwitch.App.Tray;

/// <summary>
/// The notification area icon: the application's icon file (<c>assets/icon/FolderSwitch.ico</c>, embedded as the resource
/// <c>Assets/FolderSwitch.ico</c>), at the size the notification area shows at the system's scale. The file holds an image
/// for the usual sizes (16, 20, 24, 32, 40, 48 px and more); between two of them (28 px at 175 %, 36 px at 225 %) the
/// larger one is taken, so that the system scales it down rather than up. Falls back to the standard application icon if
/// the file cannot be read.
/// </summary>
internal static class TrayIconImage
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/FolderSwitch.ico", UriKind.Absolute);

    /// <summary>The image sizes of the icon file, in ascending order (tools/FolderSwitch.IconBuilder).</summary>
    private static readonly int[] FileSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    /// <summary>The icon at the small icon size of the system DPI. The caller disposes it.</summary>
    public static DrawingIcon Create()
    {
        try
        {
            var scale = VisualTreeHelper.GetDpi(new DrawingVisual()).DpiScaleX;
            var wanted = (int)Math.Round(SystemParameters.SmallIconWidth * scale);
            var size = Array.Find(FileSizes, s => s >= wanted);
            if (size == 0)
            {
                size = FileSizes[^1];
            }

            var resource = Application.GetResourceStream(IconUri)
                ?? throw new FileNotFoundException("The icon resource is missing", IconUri.OriginalString);
            using var file = new MemoryStream();
            using (var stream = resource.Stream)
            {
                stream.CopyTo(file);
            }

            file.Position = 0;
            var icon = new DrawingIcon(file, size, size);

            // The handle is what the notification area receives: made now, so that a failure falls back here.
            _ = icon.Handle;
            return icon;
        }
        catch (Exception e)
        {
            DebugLog.Warn("The notification icon could not be loaded: the standard application icon is used", e);
            return (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        }
    }
}
