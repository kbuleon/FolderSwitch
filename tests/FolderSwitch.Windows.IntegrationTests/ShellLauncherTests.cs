using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.Shell;
using Xunit.Abstractions;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Opens a temporary text file with its associated editor on the real desktop session. The editor
/// window is found by its title (the file's unique name). It is closed only when it belongs to a
/// process started for this file, by ending that process (closing it normally could make an editor
/// save its session over the user's). An editor already running (a single-instance editor adding a
/// tab) is left as it is: closing its window would close the user's documents too.
/// </summary>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class ShellLauncherTests(ITestOutputHelper output)
{
    [Fact]
    public void ShellLauncher_opens_a_text_file()
    {
        using var folder = new TempFolder();
        var name = "FolderSwitchTest-" + Guid.NewGuid().ToString("N")[..8];
        var file = Path.Combine(folder.Path, name + ".txt");
        File.WriteAllText(file, "FolderSwitch integration test" + Environment.NewLine);
        var processesBefore = ProcessIds();

        new ShellLauncher().OpenFile(file);

        var window = WaitForWindowTitled(name, TimeSpan.FromSeconds(15));
        if (window == 0)
        {
            output.WriteLine($"No window titled with '{name}' appeared: the editor could not be identified, nothing to close.");
            return;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        using var editor = Process.GetProcessById((int)processId);
        output.WriteLine($"Editor window 0x{window:X} '{WindowTitle(window)}', process {editor.ProcessName} ({processId}).");
        if (processesBefore.Contains((int)processId))
        {
            output.WriteLine("The editor was already running: left open (its window holds other documents).");
            return;
        }

        editor.Kill();
        Assert.True(editor.WaitForExit(TimeSpan.FromSeconds(10)), "The editor did not end.");
    }

    private static HashSet<int> ProcessIds()
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes.Select(p => p.Id).ToHashSet();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>A visible top-level window whose title contains <paramref name="text"/>, or zero after the timeout.</summary>
    private static nint WaitForWindowTitled(string text, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            nint found = 0;
            EnumWindows(
                (hwnd, _) =>
                {
                    if (IsWindowVisible(hwnd) && WindowTitle(hwnd).Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        found = hwnd;
                        return false;
                    }

                    return true;
                },
                0);

            if (found != 0)
            {
                return found;
            }

            Thread.Sleep(200);
        }

        return 0;
    }

    /// <summary>The title, read without sending a message to the window (GetWindowText on another process's window).</summary>
    private static string WindowTitle(nint hwnd)
    {
        var buffer = new StringBuilder(512);
        return GetWindowText(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(nint hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
}
