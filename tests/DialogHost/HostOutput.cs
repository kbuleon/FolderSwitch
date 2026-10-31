using System.IO;

namespace DialogHost;

/// <summary>Writes whole lines to standard output, flushed at once, from any thread.</summary>
internal static class HostOutput
{
    private static readonly Lock Gate = new();

    public static void Line(string text)
    {
        lock (Gate)
        {
            try
            {
                Console.Out.WriteLine(text);
                Console.Out.Flush();
            }
            catch (IOException)
            {
                // The reader went away: nothing to report to.
            }
        }
    }

    public static void Folder(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            Line("FOLDER " + path);
        }
    }
}
