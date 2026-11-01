using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>Scripted answers to every prompt, with a record of what was asked.</summary>
internal sealed class FakeUserPrompts : IUserPrompts
{
    public bool ConfirmAnswer { get; set; } = true;

    public string? FolderToPick { get; set; }

    public string? FileToPick { get; set; }

    public string? ExportPathToPick { get; set; }

    public string? ImportPathToPick { get; set; }

    public string? UrlToAsk { get; set; }

    /// <summary>Runs while a prompt is "on screen", the way a modal dialog lets window events through.</summary>
    public Action? DuringPrompt { get; set; }

    public List<string> Confirmations { get; } = new();

    public List<string> Errors { get; } = new();

    public bool Confirm(string messageKey)
    {
        Confirmations.Add(messageKey);
        DuringPrompt?.Invoke();
        return ConfirmAnswer;
    }

    public void ShowError(string messageKey)
    {
        Errors.Add(messageKey);
        DuringPrompt?.Invoke();
    }

    public string? PickFolder()
    {
        DuringPrompt?.Invoke();
        return FolderToPick;
    }

    public string? PickFile()
    {
        DuringPrompt?.Invoke();
        return FileToPick;
    }

    public string? PickExportPath()
    {
        DuringPrompt?.Invoke();
        return ExportPathToPick;
    }

    public string? PickImportPath()
    {
        DuringPrompt?.Invoke();
        return ImportPathToPick;
    }

    public string? AskUrl()
    {
        DuringPrompt?.Invoke();
        return UrlToAsk;
    }
}
