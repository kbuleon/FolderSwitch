using System.IO;

namespace FolderSwitch.IconBuilder;

/// <summary>
/// <c>dotnet run --project tools/FolderSwitch.IconBuilder -- assets/icon</c>: draws the FolderSwitch icon
/// (<see cref="FolderSwitch.App.Views.AppIconDrawing"/>) at every size Windows asks for, and writes
/// <c>&lt;directory&gt;/png/FolderSwitch-&lt;size&gt;.png</c> and <c>&lt;directory&gt;/FolderSwitch.ico</c> (one PNG
/// image per size). The output is deterministic: the same drawing gives the same files. Exit code 0 when the files are
/// written and read back as expected, 1 for a wrong command line, 2 for any failure.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The sizes of the file: 16 to 48 cover the small icons (title bar, notification area, lists) from 100 % to 300 %
    /// scaling, 32 to 256 the larger ones (taskbar, File Explorer's views).
    /// </summary>
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    // WPF draws on an STA thread.
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: FolderSwitch.IconBuilder <output directory>   (for example: assets/icon)");
            return 1;
        }

        try
        {
            var directory = Path.GetFullPath(args[0]);
            var pngDirectory = Path.Combine(directory, "png");
            Directory.CreateDirectory(pngDirectory);

            var images = new List<(int Size, byte[] Png)>();
            foreach (var size in Sizes)
            {
                var png = IconRenderer.EncodePng(IconRenderer.Render(size));
                File.WriteAllBytes(Path.Combine(pngDirectory, $"FolderSwitch-{size}.png"), png);
                images.Add((size, png));
            }

            var icoPath = Path.Combine(directory, "FolderSwitch.ico");
            File.WriteAllBytes(icoPath, IcoFile.Build(images));

            // Read the file back, as a loader would.
            var entries = IcoFile.Read(File.ReadAllBytes(icoPath));
            if (!entries.Select(entry => entry.Width).SequenceEqual(Sizes) || entries.Any(entry => entry.Width != entry.Height))
            {
                throw new InvalidDataException("The icon does not hold the expected sizes.");
            }

            Console.WriteLine($"{icoPath}: {entries.Count} images");
            foreach (var entry in entries)
            {
                Console.WriteLine($"  {entry.Width,3} x {entry.Height,-3}  {entry.BitsPerPixel} bpp  {entry.Length,6} bytes (PNG)");
            }

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"The icon could not be built: {e.Message}");
            return 2;
        }
    }
}
