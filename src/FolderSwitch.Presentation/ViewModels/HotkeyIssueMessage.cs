using FolderSwitch.Core.Hotkeys;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A problem of the hotkey being edited, explained in the current language.</summary>
/// <param name="Issue">The problem.</param>
/// <param name="Text">Its explanation (<c>Hotkey.Issue.*</c>).</param>
/// <param name="IsBlocking">True when it forbids the hotkey (shown as a refusal); false for a warning.</param>
public sealed record HotkeyIssueMessage(HotkeyIssue Issue, string Text, bool IsBlocking);
