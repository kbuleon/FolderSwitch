using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Diagnostics.Snapshots;
using FolderSwitch.App.Startup;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Windows.SystemServices;
using Velopack;

namespace FolderSwitch.App;

/// <summary>
/// Entry point: the Velopack hooks first (they may end the process), then the single-instance check, then the WPF
/// application, which stays resident until « Quit ».
/// </summary>
/// <remarks>
/// <c>--self-test</c> composes everything with the real services, builds and renders the main window off-screen, then
/// exits with code 0 without showing anything (see <see cref="StartupOptions.SelfTest"/>). <c>--snapshot &lt;directory&gt;</c>
/// draws the window's content into PNG files, off-screen, and exits (see <see cref="SnapshotRunner"/>).
/// The debug log is in <see cref="DebugLog.DefaultDirectory"/> (<c>%APPDATA%\FolderSwitch\logs</c>) for a normal run; a
/// self-test keeps it with its configuration in its throwaway folder, a snapshot run in its output folder.
/// </remarks>
public static class Program
{
    /// <summary>The prefix of the single-instance objects of a self-test: never the one of a running FolderSwitch.</summary>
    private const string SelfTestInstanceName = "FolderSwitch.SelfTest";

    private const string SelfTestArgument = "--self-test";
    private const string SnapshotArgument = "--snapshot";
    private const string SnapshotLanguageArgument = "--snapshot-language";

    [STAThread]
    public static int Main(string[] args)
    {
        var clock = Stopwatch.StartNew();

        // Velopack must run first: on install, update and uninstall it runs the matching hook and exits. The first start
        // after an installation, or the restart after an update, goes on: it is told once FolderSwitch is ready.
        SetupNotice? setup = null;
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => DisableAutostart())
            .OnFirstRun(version => setup = new SetupNotice(SetupKind.Installed, version.ToString()))
            .OnRestarted(version => setup = new SetupNotice(SetupKind.Updated, version.ToString()))
            .Run();

        var snapshot = HasOption(args, SnapshotArgument);
        var selfTest = !snapshot && HasOption(args, SelfTestArgument);

        // A self-test never touches the user's data (configuration, log): it gets a throwaway folder, deleted at exit
        // unless the self-test failed, to keep its log.
        var selfTestDirectory = selfTest
            ? Path.Combine(Path.GetTempPath(), "FolderSwitch.SelfTest-" + Guid.NewGuid().ToString("N"))
            : null;

        var exitCode = 1;
        try
        {
            OpenLog(args, snapshot, selfTestDirectory);
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            exitCode = snapshot
                ? RunSnapshot(args)
                : RunApplication(selfTest, selfTestDirectory ?? ConfigStore.DefaultDirectory, clock, setup);
            return exitCode;
        }
        finally
        {
            // Closed first: the log of a self-test is a file of the folder that is deleted next.
            DebugLog.Close();
            if (selfTestDirectory is not null)
            {
                if (exitCode == 0)
                {
                    DeleteSelfTestFolder(selfTestDirectory);
                }
                else
                {
                    Console.Error.WriteLine($"Self-test failed (exit code {exitCode}); its log is kept in {selfTestDirectory}");
                }
            }
        }
    }

    /// <summary>
    /// Opens the debug log: in the data folder of the user for a normal run, in the throwaway folder for a self-test,
    /// in the output folder for a snapshot run (which has none when its argument is missing: no log then). Nothing of
    /// a self-test or of a snapshot run is ever written to the user's data folder.
    /// </summary>
    private static void OpenLog(string[] args, bool snapshot, string? selfTestDirectory)
    {
        if (snapshot)
        {
            if (OptionValue(args, SnapshotArgument) is { } outputDirectory)
            {
                DebugLog.Open(outputDirectory);
            }

            return;
        }

        DebugLog.Open(selfTestDirectory ?? DebugLog.DefaultDirectory);
    }

    /// <summary>The WPF application: single instance, composition, then the dispatcher until « Quit ». The exit code.</summary>
    private static int RunApplication(bool selfTest, string configDirectory, Stopwatch clock, SetupNotice? setup)
    {
        DebugLog.Info($"Starting FolderSwitch {typeof(Program).Assembly.GetName().Version} on Windows {Environment.OSVersion.Version}"
            + (selfTest ? " (self-test)" : string.Empty)
            + (setup is null ? string.Empty : $" ({setup.Kind} {setup.Version})"));

        // The UI thread's context, created before anything that posts to it: the services raise their events there.
        // Posts made before the dispatcher runs wait in its queue.
        var uiContext = new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher);

        SingleInstance? instance;
        try
        {
            instance = SingleInstance.TryAcquire(selfTest ? SelfTestInstanceName : "FolderSwitch", uiContext);
        }
        catch (Exception e)
        {
            DebugLog.Error("The single-instance check failed", e);
            return 1;
        }

        if (instance is null)
        {
            // A self-test only meets another self-test (its objects have their own name): that is no pass.
            DebugLog.Info(selfTest
                ? "Self-test: another self-test is running"
                : "Another instance is running: it was asked to show itself");
            return selfTest ? 1 : 0;
        }

        App? app = null;
        try
        {
            app = new App(new StartupOptions(selfTest, configDirectory, clock, uiContext, instance, setup));
            app.InitializeComponent();
            var exitCode = app.Run();
            DebugLog.Info($"Exit with code {exitCode}");
            return exitCode;
        }
        catch (Exception e)
        {
            // A critical error the dispatcher let through (see App): the process ends here. The keyboard hook and the
            // notification icon are released if that is still possible; the operating system does the rest.
            DebugLog.Error("FolderSwitch stopped on an error", e);
            app?.ReleaseAfterFailure();
            return 1;
        }
        finally
        {
            instance.Dispose();
        }
    }

    /// <summary>
    /// <c>--snapshot &lt;directory&gt; [--snapshot-language &lt;code&gt;]</c>: draws the main window's content into PNG files,
    /// off-screen, and exits (see <see cref="SnapshotRunner"/>). Needs no single instance: it touches nothing of a
    /// running FolderSwitch, nor the configuration. Its log is <c>folderswitch.log</c> in the output folder.
    /// </summary>
    private static int RunSnapshot(string[] args)
    {
        if (OptionValue(args, SnapshotArgument) is not { } directory)
        {
            DebugLog.Error($"{SnapshotArgument} needs a directory");
            Console.Error.WriteLine($"Usage: {SnapshotArgument} <directory> [{SnapshotLanguageArgument} <code>]");
            return 1;
        }

        DebugLog.Info($"Snapshot to {directory}");
        return SnapshotRunner.Run(directory, OptionValue(args, SnapshotLanguageArgument));
    }

    private static bool HasOption(string[] args, string name) =>
        args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The argument that follows <paramref name="name"/>, when there is one and it is not another option.</summary>
    private static string? OptionValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[index + 1]
            : null;
    }

    private static void DeleteSelfTestFolder(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            DebugLog.Warn($"Self-test: the throwaway configuration folder {directory} could not be deleted", e);
        }
    }

    /// <summary>Velopack's before-uninstall hook: FolderSwitch must no longer start with Windows.</summary>
    private static void DisableAutostart()
    {
        try
        {
            new Autostart().Set(false, string.Empty);
        }
        catch (Exception)
        {
            // The uninstaller must go on whatever happens; nothing to report it to.
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        DebugLog.Error($"Unhandled exception (terminating: {e.IsTerminating})", e.ExceptionObject as Exception);

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        DebugLog.Error("Unobserved task exception", e.Exception);
        e.SetObserved();
    }
}
