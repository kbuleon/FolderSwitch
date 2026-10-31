namespace FolderSwitch.Core.Hotkeys;

/// <summary>A problem found with a candidate hotkey. See <see cref="HotkeyRules.IsBlocking"/>.</summary>
public enum HotkeyIssue
{
    /// <summary>A single modifier key alone (e.g. Ctrl): too easy to trigger by accident.</summary>
    SingleModifier,

    /// <summary>No modifier at all: the hotkey would swallow ordinary typing.</summary>
    NoModifier,

    /// <summary>A combination Windows keeps for itself (Win+L, Alt+Tab, ...).</summary>
    ReservedByWindows,

    /// <summary>Ctrl+Alt is what Windows sends for AltGr on many keyboard layouts. Warning only.</summary>
    CtrlAltLikeAltGr,

    /// <summary>Registration failed because another application already owns the combination.</summary>
    TakenByOtherApp,
}
