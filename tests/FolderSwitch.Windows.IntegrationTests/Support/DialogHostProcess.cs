using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// A running <c>tests/DialogHost</c>: one file dialog on a given folder, reporting on its standard
/// output (<c>READY &lt;hwnd&gt;</c>, <c>FOLDER &lt;path&gt;</c>...). Dispose sends <c>quit</c>, then kills the
/// process if it has not exited after a few seconds.
/// </summary>
internal sealed class DialogHostProcess : IDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);

    private readonly Process _process;
    private readonly BlockingCollection<string> _lines = new();
    private readonly ConcurrentQueue<string> _transcript = new();

    private DialogHostProcess(Process process)
    {
        _process = process;
        var reader = new Thread(ReadOutput) { IsBackground = true, Name = "DialogHost output" };
        reader.Start();
    }

    /// <summary>The dialog window, once <see cref="WaitForReady"/> has returned.</summary>
    public nint Dialog { get; private set; }

    /// <summary>Every line read so far, for failure messages.</summary>
    public string Transcript => string.Join(" | ", _transcript);

    public static string ExecutablePath
    {
        get
        {
            var path = typeof(DialogHostProcess).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(a => a.Key == "DialogHostPath")
                .Value!;
            Assert.True(File.Exists(path), "DialogHost.exe not found at " + path);
            return path;
        }
    }

    /// <summary>Starts DialogHost and waits until its dialog is shown.</summary>
    public static DialogHostProcess Start(string mode, string folder, string? fileName = null)
    {
        var info = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(mode);
        info.ArgumentList.Add(folder);
        if (fileName is not null)
        {
            info.ArgumentList.Add(fileName);
        }

        var host = new DialogHostProcess(Process.Start(info)!);
        try
        {
            host.WaitForReady();
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <summary>The next line matching <paramref name="predicate"/>, or <c>null</c> after the timeout.</summary>
    public string? WaitForLine(Func<string, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero || !_lines.TryTake(out var line, left))
            {
                return null;
            }

            if (predicate(line))
            {
                return line;
            }
        }
    }

    /// <summary>Whether the process has ended (closed its dialog, or crashed).</summary>
    public bool HasExited => _process.HasExited;

    /// <summary>Sends <c>quit</c> and returns the exit code (0: the dialog closed normally).</summary>
    public int QuitAndWait()
    {
        if (!_process.HasExited)
        {
            Send("quit");
        }

        Assert.True(_process.WaitForExit(TimeSpan.FromSeconds(10)), "DialogHost did not exit after quit.");
        return _process.ExitCode;
    }

    /// <summary>Writes a command line to DialogHost's standard input.</summary>
    public void Send(string command)
    {
        _process.StandardInput.WriteLine(command);
        _process.StandardInput.Flush();
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.WriteLine("quit");
                _process.StandardInput.Close();
            }
        }
        catch (IOException)
        {
            // Already gone.
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        if (!_process.WaitForExit(TimeSpan.FromSeconds(5)))
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(TimeSpan.FromSeconds(5));
        }

        _process.Dispose();
    }

    private void WaitForReady()
    {
        var ready = WaitForLine(l => l.StartsWith("READY ", StringComparison.Ordinal) || l.StartsWith("ERROR", StringComparison.Ordinal), ReadyTimeout);
        Assert.True(ready is not null && ready.StartsWith("READY ", StringComparison.Ordinal), "DialogHost did not get ready: " + Transcript);
        Dialog = (nint)Convert.ToInt64(ready["READY ".Length..], 16);
    }

    private void ReadOutput()
    {
        try
        {
            while (_process.StandardOutput.ReadLine() is { } line)
            {
                _transcript.Enqueue(line);
                _lines.Add(line);
            }
        }
        catch (Exception)
        {
            // Process gone.
        }
    }
}
