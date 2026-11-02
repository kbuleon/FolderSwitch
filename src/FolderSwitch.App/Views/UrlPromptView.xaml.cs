// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The content of the window that asks for a web address (<see cref="PromptWindow"/>): its texts come from the
/// localizer, its field and its state from <see cref="UrlPromptViewModel"/>. The field has a menu of its own, in the
/// language of the application (the text boxes of the theme have none), so that an address can be pasted with the mouse.
/// </summary>
public partial class UrlPromptView : UserControl, IPromptContent
{
    private readonly UrlPromptViewModel _viewModel;

    internal UrlPromptView(UrlPromptViewModel viewModel, Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localize);

        _viewModel = viewModel;
        TitleLabel = localize("UrlPrompt.Title");
        AddressLabel = localize("UrlPrompt.Address");
        AddLabel = localize("UrlPrompt.Add");
        CancelLabel = localize("UrlPrompt.Cancel");
        InvalidMessage = localize("UrlPrompt.Invalid");
        DataContext = viewModel;

        InitializeComponent();
        AddressBox.ContextMenu = TextMenu(AddressBox, localize);
    }

    /// <summary>Raised with the answer: true for « Add », false for « Cancel » or Escape.</summary>
    public event EventHandler<bool>? Answered;

    public string TitleLabel { get; }

    public string AddressLabel { get; }

    public string AddLabel { get; }

    public string CancelLabel { get; }

    public string InvalidMessage { get; }

    /// <summary>The keyboard starts in the field, with its text selected, so that typing replaces it.</summary>
    void IPromptContent.FocusStart()
    {
        AddressBox.Focus();
        AddressBox.SelectAll();
    }

    private static ContextMenu TextMenu(TextBox box, Func<string, string> localize)
    {
        MenuItem Entry(RoutedUICommand command, string key) =>
            new() { Header = localize(key), Command = command, CommandTarget = box };

        return new ContextMenu
        {
            Items =
            {
                Entry(ApplicationCommands.Cut, "Text.Cut"),
                Entry(ApplicationCommands.Copy, "Text.Copy"),
                Entry(ApplicationCommands.Paste, "Text.Paste"),
                new Separator(),
                Entry(ApplicationCommands.SelectAll, "Text.SelectAll"),
            },
        };
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Accept();
        Answered?.Invoke(this, _viewModel.Result is not null);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Answered?.Invoke(this, false);
}
