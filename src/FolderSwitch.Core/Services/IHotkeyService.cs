using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Services;

/// <summary>
/// The global shortcut that summons FolderSwitch: either modifier keys alone (the default Ctrl + Win,
/// fired when they are released) or modifiers plus a regular key. Methods may be called from any thread.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Raised when the registered hotkey fires. The implementation documents the thread it is raised on.</summary>
    event EventHandler? Triggered;

    /// <summary>
    /// Validates <paramref name="h"/> with <see cref="HotkeyRules.Validate"/> then registers it in place
    /// of the current hotkey. Returns the issues found: with a blocking one (see
    /// <see cref="HotkeyRules.IsBlocking"/>) nothing is registered; <see cref="HotkeyIssue.TakenByOtherApp"/>
    /// is added when the system refuses the combination. The other issues are only warnings.
    /// </summary>
    IReadOnlyList<HotkeyIssue> Register(Hotkey h);

    /// <summary>Removes the current hotkey, if any.</summary>
    void Unregister();

    /// <summary>
    /// Captures the next combination the user presses instead of firing the hotkey: modifiers pressed
    /// then released alone (at least two) give a modifier-only hotkey, modifiers plus a regular key give
    /// that combination. <paramref name="onCaptured"/> is called once, then capture ends by itself;
    /// disposing the result ends it earlier, without a callback.
    /// </summary>
    IDisposable BeginCapture(Action<Hotkey> onCaptured);

    /// <summary>
    /// Whether <paramref name="h"/> could be registered right now: <c>false</c> when another application
    /// already owns the combination. Always <c>true</c> for a modifier-only hotkey, which reserves nothing.
    /// </summary>
    bool Probe(Hotkey h);
}
