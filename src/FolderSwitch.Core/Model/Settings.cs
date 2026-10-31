namespace FolderSwitch.Core.Model;

public sealed class Settings
{
    /// <summary>UI language code; <c>null</c> means follow the system language.</summary>
    public string? Language { get; set; }

    public Hotkey Hotkey { get; set; } = Hotkey.Default;

    public bool StartWithWindows { get; set; } = true;

    public bool FirstRunCompleted { get; set; }

    public Guid? ActiveTabId { get; set; }
}
