// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Views;
using FolderSwitch.Core.Localization;
using FolderSwitch.Presentation.Services;
using FolderSwitch.Presentation.ViewModels;
using Microsoft.Win32;

namespace FolderSwitch.App.Services;

/// <summary>
/// The modal questions and pickers: questions, errors and notices in FolderSwitch's own themed message box
/// (<see cref="PromptWindow"/>), folders and files in the Windows pickers (<see cref="OpenFolderDialog"/>,
/// <see cref="OpenFileDialog"/>, <see cref="SaveFileDialog"/>). Each is owned by the main window while it is visible,
/// so that the focus goes back to it when the box closes; with the window hidden (a message at startup) it has no
/// owner.
/// </summary>
/// <remarks>
/// UI thread only, except <see cref="ShowErrorLater"/>. Silent (a self-test): nothing is ever shown, every message is
/// logged instead, questions are answered « no » and pickers are cancelled.
/// </remarks>
internal sealed class UserPrompts : IUserPrompts
{
    private const string JsonFilter = "JSON (*.json)|*.json";
    private const string ExportFileName = "FolderSwitch.json";

    private readonly Localizer _localizer;
    private readonly Func<Window?> _owner;
    private readonly Dispatcher _dispatcher;
    private readonly bool _silent;
    private int _open;

    /// <param name="owner">The main window, once it exists.</param>
    /// <param name="silent">Never show anything (a self-test).</param>
    public UserPrompts(Localizer localizer, Func<Window?> owner, bool silent)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(owner);

        _localizer = localizer;
        _owner = owner;
        _silent = silent;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>
    /// True while a box or a picker is open. It takes the focus from the main window, whose deactivation must then not
    /// hide it (see <c>WindowController</c>).
    /// </summary>
    public bool IsOpen => _open > 0;

    public bool Confirm(string messageKey) => Run(() => Show(messageKey, PromptKind.Confirm));

    public void ShowError(string messageKey) => Run(() => Show(messageKey, PromptKind.Error));

    /// <summary>An information message (not an error), such as a configuration restored at startup.</summary>
    public void ShowInfo(string messageKey) => Run(() => Show(messageKey, PromptKind.Info));

    /// <summary>
    /// An information message already localized and formatted (<paramref name="message"/>), such as the end of an
    /// installation; <paramref name="messageKey"/> is what the log says when nothing is shown.
    /// </summary>
    public void ShowInfo(string messageKey, string message) => Run(() => Present(messageKey, message, PromptKind.Info));

    /// <summary>
    /// Offers to install the update <paramref name="version"/>: true for « Install », false for « Later » (also the
    /// default answer, and Escape).
    /// </summary>
    public bool OfferUpdate(string version) =>
        Run(() => Present("Update.Prompt", _localizer.Format("Update.Prompt", version), PromptKind.Update,
            _localizer["Update.Install"], _localizer["Update.Later"]));

    /// <summary>Shows an error once the current operation is over: callable in the middle of an edit, from any thread.</summary>
    public void ShowErrorLater(string messageKey) =>
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () => ShowError(messageKey));

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Multiselect = false };
        return Run(() => ShowDialog(dialog)) ? dialog.FolderName : null;
    }

    public string? PickFile()
    {
        var dialog = new OpenFileDialog { Multiselect = false, CheckFileExists = true };
        return Run(() => ShowDialog(dialog)) ? dialog.FileName : null;
    }

    public string? PickExportPath()
    {
        var dialog = new SaveFileDialog
        {
            Filter = JsonFilter,
            DefaultExt = ".json",
            AddExtension = true,
            FileName = ExportFileName,
            OverwritePrompt = true,
        };
        return Run(() => ShowDialog(dialog)) ? dialog.FileName : null;
    }

    public string? PickImportPath()
    {
        var dialog = new OpenFileDialog { Filter = JsonFilter, Multiselect = false, CheckFileExists = true };
        return Run(() => ShowDialog(dialog)) ? dialog.FileName : null;
    }

    /// <summary>
    /// Asks for a web address in a small window of the theme, the field starting with <paramref name="prefill"/>, or
    /// with the clipboard when it holds a web address and nothing is given (a clipboard that cannot be read is
    /// ignored). Silent: cancelled.
    /// </summary>
    public string? AskUrl(string? prefill = null)
    {
        if (_silent)
        {
            DebugLog.Info("Web address prompt not shown (silent)");
            return null;
        }

        var viewModel = new UrlPromptViewModel(prefill ?? ReadClipboardText());
        var view = new UrlPromptView(viewModel, key => _localizer[key]);
        return Run(() => new PromptWindow(view, VisibleOwner()).Ask()) ? viewModel.Result : null;
    }

    /// <summary>The main window when it is on screen; otherwise no owner (a hidden owner would hide the box with it).</summary>
    private Window? VisibleOwner() => _owner() is { IsVisible: true } window ? window : null;

    /// <summary>Shows the box; true when a question was answered « yes » (an error or a notice gives false).</summary>
    private bool Show(string messageKey, PromptKind kind) => Present(messageKey, _localizer[messageKey], kind);

    /// <param name="messageKey">What the log says when nothing is shown.</param>
    /// <param name="message">The text, already localized and formatted.</param>
    private bool Present(string messageKey, string message, PromptKind kind, string? yesText = null, string? noText = null)
    {
        if (_silent)
        {
            DebugLog.Info($"Message not shown (silent): {messageKey}");
            return false;
        }

        var view = new PromptView(kind, message, key => _localizer[key], yesText, noText);
        return new PromptWindow(view, VisibleOwner()).Ask();
    }

    /// <summary>The text on the clipboard; <c>null</c> when there is none or when the clipboard is busy.</summary>
    private static string? ReadClipboardText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            DebugLog.Warn($"The clipboard could not be read: {exception.Message}");
            return null;
        }
    }

    private bool ShowDialog(CommonDialog dialog) =>
        !_silent && (VisibleOwner() is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;

    private T Run<T>(Func<T> prompt)
    {
        _open++;
        try
        {
            return prompt();
        }
        finally
        {
            _open--;
        }
    }
}
