// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Themes;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.App.Views;

/// <summary>What <see cref="PromptWindow"/> needs from its content: an answer, and where the keyboard starts.</summary>
internal interface IPromptContent
{
    /// <summary>Raised with the answer: true for the affirmative button, false for anything else.</summary>
    event EventHandler<bool>? Answered;

    /// <summary>Puts the keyboard where the user starts (the default button, a field...).</summary>
    void FocusStart();
}

/// <summary>
/// A message box of FolderSwitch's own, in the theme's colors (<see cref="PromptView"/>), in place of the system's.
/// It also hosts the window that asks for a web address (<see cref="UrlPromptView"/>).
/// Borderless like the main window: rounded and shadowed by the Desktop Window Manager on Windows 11, with a 1 px
/// border on Windows 10. Modal; owned by the main window when it is on screen (centered on it, and the focus goes back
/// to it afterwards), else centered on the screen and listed in the taskbar so that it can be found.
/// </summary>
internal sealed class PromptWindow : Window
{
    private readonly IPromptContent _view;
    private bool _answer;

    public PromptWindow(PromptView view, Window? owner)
        : this(view, view, owner)
    {
    }

    public PromptWindow(UrlPromptView view, Window? owner)
        : this(view, view, owner)
    {
    }

    private PromptWindow(UserControl content, IPromptContent view, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
        Title = "FolderSwitch";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        ShowInTaskbar = owner is null;
        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        SetResourceReference(BackgroundProperty, ThemeKeys.Menu);
        SetResourceReference(BorderBrushProperty, ThemeKeys.WindowBorder);
        BorderThickness = WindowFrame.SupportsRoundedCorners ? new Thickness(0) : new Thickness(1);
        Content = content;

        view.Answered += OnAnswered;
        Loaded += (_, _) => view.FocusStart();
    }

    /// <summary>Shows the box until it is answered: true for « Yes », false for anything else.</summary>
    public bool Ask()
    {
        ShowDialog();
        return _answer;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (WindowFrame.SupportsRoundedCorners && !WindowFrame.SetRoundedCorners(hwnd, small: false))
        {
            DebugLog.Warn("The corners of a message box could not be rounded");
        }

        WindowFrame.EnableShadow(hwnd);
    }

    protected override void OnClosed(EventArgs e)
    {
        _view.Answered -= OnAnswered;
        base.OnClosed(e);
    }

    private void OnAnswered(object? sender, bool yes)
    {
        _answer = yes;
        DialogResult = yes;
    }
}
