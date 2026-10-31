using System.Diagnostics;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.App.Startup;

/// <summary>What <see cref="Program.Main"/> hands to the application.</summary>
/// <param name="SelfTest">
/// <c>--self-test</c>: everything is composed with the real services and the main window is built and rendered
/// off-screen, then the application exits with code 0, without showing anything. The configuration lives in a
/// throwaway folder (<paramref name="ConfigDirectory"/>), the notification icon is built but not shown, no user trigger
/// is wired (a press of the shortcut does nothing, although its keyboard hook is installed), no message is shown and
/// the first run is not completed (it would write the user's autostart entry).
/// </param>
/// <param name="ConfigDirectory">
/// Where the configuration is kept: <see cref="FolderSwitch.Core.Persistence.ConfigStore.DefaultDirectory"/>, or for a
/// self-test a throwaway folder (it holds the log of the self-test too) that <see cref="Program.Main"/> deletes at exit
/// unless the self-test failed.
/// </param>
/// <param name="Clock">Started at the very beginning of <see cref="Program.Main"/>: measures the startup.</param>
/// <param name="UiContext">The UI thread's synchronization context, where the services raise their events.</param>
/// <param name="Instance">The single-instance ownership; disposed by <see cref="Program.Main"/> at the very end.</param>
internal sealed record StartupOptions(
    bool SelfTest,
    string ConfigDirectory,
    Stopwatch Clock,
    SynchronizationContext UiContext,
    SingleInstance Instance);
