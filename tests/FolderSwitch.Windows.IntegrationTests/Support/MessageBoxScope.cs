using System.Runtime.InteropServices;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>A message box shown by a thread of the test process, closed on dispose.</summary>
internal sealed class MessageBoxScope : IDisposable
{
    private const uint MbOk = 0x0;
    private const uint WmClose = 0x0010;

    private readonly Thread _thread;

    private MessageBoxScope(string caption)
    {
        _thread = new Thread(() => MessageBox(0, "FolderSwitch integration test", caption, MbOk))
        {
            IsBackground = true,
            Name = "Test message box",
        };
        _thread.Start();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (Window == 0 && DateTime.UtcNow < deadline)
        {
            Window = FindWindow("#32770", caption);
            Thread.Sleep(20);
        }

        if (Window == 0)
        {
            Dispose();
            Assert.Fail("The message box did not appear.");
        }
    }

    public nint Window { get; }

    public static MessageBoxScope Show() => new("FolderSwitch test " + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 50 && _thread.IsAlive; attempt++)
        {
            if (Window != 0)
            {
                PostMessage(Window, WmClose, 0, 0);
            }

            _thread.Join(100);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(nint owner, string text, string caption, uint type);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
    private static extern nint FindWindow(string className, string windowName);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);
}
