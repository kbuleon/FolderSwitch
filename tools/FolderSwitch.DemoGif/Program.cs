// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;

namespace FolderSwitch.DemoGif;

/// <summary>
/// <c>FolderSwitch.DemoGif &lt;snapshot directory&gt; &lt;language&gt; &lt;output.gif&gt; [--frames &lt;directory&gt;]</c>:
/// draws the README's animated tour of FolderSwitch in one language, from the PNG files of the application's
/// diagnostic mode (<c>FolderSwitch.exe --snapshot &lt;directory&gt; --snapshot-language &lt;language&gt;</c>, 144 DPI ones)
/// and the captions of <c>Captions/&lt;language&gt;.json</c>. Eight scenes, each held a few seconds and joined by short
/// crossfades; the animation loops forever. <c>--frames</c> also writes each scene, and the middle frame of each
/// crossfade, as PNG files for review. Exit code 0 when the GIF is written, 1 for a wrong command line, 2 for any
/// failure. <c>build/readme-assets.ps1</c> runs it for every language.
/// </summary>
internal static class Program
{
    // Three frames of 80 ms between two scenes.
    private static readonly double[] Crossfade = [0.25, 0.5, 0.75];
    private const int CrossfadeDelay = 8;

    // WPF draws on an STA thread.
    [STAThread]
    private static int Main(string[] args)
    {
        if (!TryParse(args, out var snapshots, out var language, out var output, out var framesDirectory))
        {
            Console.Error.WriteLine("Usage: FolderSwitch.DemoGif <snapshot directory> <language> <output.gif> [--frames <directory>]");
            return 1;
        }

        try
        {
            var captions = Captions.Load(language);
            var scenes = new Tour(Path.GetFullPath(snapshots), language, captions).Build();

            var frames = new List<Frame>();
            for (var i = 0; i < scenes.Count; i++)
            {
                frames.Add(new Frame(scenes[i].Image, scenes[i].Delay));

                // The last scene fades into the first one: the loop goes on without a jump.
                var next = scenes[(i + 1) % scenes.Count];
                frames.AddRange(Crossfade.Select(amount => new Frame(Painter.Blend(scenes[i].Image, next.Image, amount), CrossfadeDelay, Transient: true)));
            }

            if (framesDirectory is not null)
            {
                WriteFrames(Path.GetFullPath(framesDirectory), scenes, frames);
            }

            output = Path.GetFullPath(output);
            GifEncoder.Write(frames, output);

            var (count, seconds) = GifEncoder.Read(output);
            if (count == 0)
            {
                throw new InvalidDataException("The GIF holds no frame.");
            }

            var size = new FileInfo(output).Length;
            Console.WriteLine($"{output}: {count} frames, {seconds:0.0} s, {size / 1048576.0:0.00} MB ({size} bytes)");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"The tour could not be drawn: {e.Message}");
            return 2;
        }
    }

    private static void WriteFrames(string directory, IReadOnlyList<Scene> scenes, List<Frame> frames)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(directory, "*.png"))
        {
            File.Delete(file);
        }

        var perScene = 1 + Crossfade.Length;
        for (var i = 0; i < scenes.Count; i++)
        {
            File.WriteAllBytes(Path.Combine(directory, scenes[i].Name + ".png"), GifEncoder.PngBytes(scenes[i].Image));
            var fade = frames[(i * perScene) + 1 + (Crossfade.Length / 2)];
            File.WriteAllBytes(Path.Combine(directory, $"{scenes[i].Name}-fade.png"), GifEncoder.PngBytes(fade.Image));
        }
    }

    private static bool TryParse(string[] args, out string snapshots, out string language, out string output, out string? frames)
    {
        snapshots = language = output = string.Empty;
        frames = null;
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--frames")
            {
                if (i + 1 >= args.Length)
                {
                    return false;
                }

                frames = args[++i];
            }
            else if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        if (positional.Count != 3)
        {
            return false;
        }

        (snapshots, language, output) = (positional[0], positional[1], positional[2]);
        return true;
    }
}
