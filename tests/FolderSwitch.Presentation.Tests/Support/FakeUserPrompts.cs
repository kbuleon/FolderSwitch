// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

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

    /// <summary>The address prompt is confirmed as it was prefilled; <see cref="UrlToAsk"/> answers it otherwise.</summary>
    public bool AcceptsUrlPrefill { get; set; }

    /// <summary>What each address prompt was prefilled with, <c>null</c> for nothing.</summary>
    public List<string?> UrlPrefills { get; } = new();

    /// <summary>Runs while a prompt is "on screen", the way a modal dialog lets window events through.</summary>
    public Action? DuringPrompt { get; set; }

    /// <summary>The initial folder each file picker was given, <c>null</c> for none.</summary>
    public List<string?> PickFileFolders { get; } = new();

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

    public string? PickFile(string? initialFolder)
    {
        PickFileFolders.Add(initialFolder);
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

    public string? AskUrl(string? prefill = null)
    {
        UrlPrefills.Add(prefill);
        DuringPrompt?.Invoke();
        return AcceptsUrlPrefill ? prefill : UrlToAsk;
    }
}
