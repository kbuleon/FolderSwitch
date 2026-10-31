namespace FolderSwitch.Core.Hotkeys;

/// <summary>What the keyboard hook must do after a key event fed to <see cref="ModifierChordDetector"/>.</summary>
/// <param name="Triggered">The chord was completed and fully released: fire the hotkey.</param>
/// <param name="SuppressStartMenu">
/// The Win key is being released at the end of a clean chord: inject a mask key before letting this
/// release through, so Windows does not open the Start menu.
/// </param>
public readonly record struct ChordStep(bool Triggered, bool SuppressStartMenu);
