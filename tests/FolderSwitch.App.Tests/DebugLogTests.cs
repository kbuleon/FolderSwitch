using System.Text;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Tests.Support;

namespace FolderSwitch.App.Tests;

/// <summary>
/// The rolling debug log, opened in a temporary folder (never the user's data folder) and closed after each test.
/// </summary>
public sealed class DebugLogTests : IDisposable
{
    /// <summary>About 1 KiB per line: a few hundred lines pass the roll size.</summary>
    private static readonly string LongMessage = new('x', 1000);

    private readonly TempDirectory _temp = new();

    public DebugLogTests()
    {
        DebugLog.Close();
        DebugLog.Open(_temp.Root);
    }

    private string Current => _temp.File("folderswitch.log");

    private string Previous => _temp.File("folderswitch.1.log");

    public void Dispose()
    {
        DebugLog.Close();
        _temp.Dispose();
    }

    [Fact]
    public void Opens_in_the_folder_given_and_writes_lines()
    {
        DebugLog.Info("hello");
        DebugLog.Warn("careful", new InvalidOperationException("boom"));

        Assert.Equal(Current, DebugLog.FilePath);
        var text = ReadShared(Current);
        Assert.Contains("INFO  [", text, StringComparison.Ordinal);
        Assert.Contains("] hello", text, StringComparison.Ordinal);
        Assert.Contains("WARN  [", text, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Past_the_size_limit_the_file_rolls_over()
    {
        WriteUntil(() => new FileInfo(Current).Length > DebugLog.MaxBytes);
        DebugLog.Info("after the roll");

        Assert.True(File.Exists(Previous));
        Assert.True(new FileInfo(Previous).Length > DebugLog.MaxBytes);
        Assert.True(new FileInfo(Current).Length < 4 * 1024, $"{new FileInfo(Current).Length} bytes");
        Assert.Contains("after the roll", ReadShared(Current), StringComparison.Ordinal);
    }

    [Fact]
    public void A_roll_that_fails_keeps_logging_in_the_current_file_then_rolls_later()
    {
        File.WriteAllText(Previous, "held by another tool");
        using (new FileStream(Previous, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // folderswitch.1.log cannot be replaced while that handle (no delete sharing) is open.
            WriteUntil(() => new FileInfo(Current).Length > DebugLog.MaxBytes);
            DebugLog.Info("still logging");

            var text = ReadShared(Current);
            Assert.Contains("could not roll over", text, StringComparison.Ordinal);
            Assert.Contains("still logging", text, StringComparison.Ordinal);
            Assert.Equal("held by another tool", ReadShared(Previous));
        }

        // Released: the roll is tried again once the file has grown a little more.
        var sizeBeforeRetry = new FileInfo(Current).Length;
        WriteUntil(() => !File.Exists(Current) || new FileInfo(Current).Length < sizeBeforeRetry);
        DebugLog.Info("after the late roll");

        Assert.True(new FileInfo(Previous).Length > DebugLog.MaxBytes);
        Assert.Contains("after the late roll", ReadShared(Current), StringComparison.Ordinal);
        Assert.DoesNotContain("could not roll over", ReadShared(Current), StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_roll_does_not_retry_on_every_line()
    {
        File.WriteAllText(Previous, "held by another tool");
        using var held = new FileStream(Previous, FileMode.Open, FileAccess.Read, FileShare.Read);

        WriteUntil(() => new FileInfo(Current).Length > DebugLog.MaxBytes);
        for (var i = 0; i < 10; i++)
        {
            DebugLog.Info("short line");
        }

        var warnings = ReadShared(Current).Split('\n').Count(line => line.Contains("could not roll over", StringComparison.Ordinal));
        Assert.Equal(1, warnings);
    }

    [Fact]
    public void After_Close_entries_are_dropped()
    {
        DebugLog.Info("before");
        DebugLog.Close();

        DebugLog.Info("after");

        var text = ReadShared(Current);
        Assert.Contains("before", text, StringComparison.Ordinal);
        Assert.DoesNotContain("after", text, StringComparison.Ordinal);
    }

    /// <summary>Writes long lines until <paramref name="done"/> (bounded, so that a broken log cannot loop forever).</summary>
    private static void WriteUntil(Func<bool> done)
    {
        for (var i = 0; i < 5000 && !done(); i++)
        {
            DebugLog.Info(LongMessage);
        }

        Assert.True(done(), "The log never reached the expected state.");
    }

    /// <summary>The file's text, read while the log may still hold it open for writing.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
