using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DialogHost;

/// <summary>
/// Test host for the Windows dialog integration tests: opens one file dialog on a given folder, owned
/// by a small host window, and reports on standard output.
/// </summary>
/// <remarks>
/// <para>Usage: <c>DialogHost &lt;mode&gt; &lt;initialFolder&gt; [fileName]</c>, where mode is one of
/// <c>open</c>, <c>save</c>, <c>folder</c> (Common Item Dialog, COM <c>IFileOpenDialog</c> /
/// <c>IFileSaveDialog</c>), <c>legacy-open</c>, <c>legacy-save</c> (comdlg32
/// <c>GetOpenFileName</c> / <c>GetSaveFileName</c>, Explorer style with a hook, which keeps the
/// pre-Vista look) or <c>legacy-folder</c> (<c>SHBrowseForFolder</c>, new dialog style).
/// <c>legacy-folder-classic</c> is the same picker without <c>BIF_NEWDIALOGSTYLE</c>. The optional
/// file name pre-fills the file name box.</para>
/// <para>Output lines: <c>READY &lt;hwnd-hex&gt;</c> once the dialog is visible; <c>FOLDER &lt;path&gt;</c>
/// each time the dialog reports a folder change (parsing form, <c>::{CLSID}</c> for a virtual
/// location); <c>CLOSED</c> when the dialog has closed; <c>ERROR &lt;message&gt;</c> on failure (exit
/// code 1).</para>
/// <para>It exits when the dialog closes, or when standard input receives <c>quit</c> or is closed
/// (the dialog is then cancelled). <c>hang &lt;ms&gt;</c> on standard input blocks the dialog's thread
/// for that long, reported by <c>HUNG</c> then <c>AWAKE</c>; <c>slow &lt;ms&gt;</c> holds the next message
/// that reads the folder through a pointer, once taken, for that long (<see cref="MessageDelay"/>);
/// <c>drop</c> / <c>drop reset</c> drops the next validation (OK, or a folder picker's selection
/// message), reported by <c>DROPPED</c>, then <c>PASSED</c> for each later one; <c>slow-ok &lt;ms&gt;</c>
/// holds the next OK that long, then handles it through a posted message, after anything sent
/// meanwhile, reported by <c>SLOW OK</c> then <c>PASSED</c> (<see cref="ValidationDrop"/>).</para>
/// </remarks>
public partial class App : Application
{
    private DialogSession? _session;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!DialogRequest.TryParse(e.Args, out var request, out var error))
        {
            HostOutput.Line("ERROR " + error);
            Shutdown(1);
            return;
        }

        var host = new Window
        {
            Title = "DialogHost " + request.Mode,
            Width = 320,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowActivated = true,

            // ContentRendered is only raised for a window that has content.
            Content = new TextBlock { Text = "DialogHost " + request.Mode, Margin = new Thickness(12) },
        };
        host.ContentRendered += (_, _) => RunDialog(host, request);
        host.Show();
    }

    private void RunDialog(Window host, DialogRequest request)
    {
        var owner = new WindowInteropHelper(host).Handle;
        _session = new DialogSession(owner);
        var exitCode = 0;
        try
        {
            _session.Start();
            switch (request.Mode)
            {
                case DialogMode.Open:
                case DialogMode.Save:
                case DialogMode.Folder:
                    ModernDialog.Show(request, owner);
                    break;
                case DialogMode.LegacyOpen:
                case DialogMode.LegacySave:
                    LegacyFileDialog.Show(request, owner);
                    break;
                case DialogMode.LegacyFolder:
                case DialogMode.LegacyFolderClassic:
                    LegacyFolderPicker.Show(request, owner);
                    break;
            }

            HostOutput.Line("CLOSED");
        }
        catch (Exception ex)
        {
            HostOutput.Line("ERROR " + ex.GetType().Name + ": " + ex.Message.ReplaceLineEndings(" "));
            exitCode = 1;
        }
        finally
        {
            _session.Stop();
        }

        host.Close();
        Shutdown(exitCode);
    }
}
