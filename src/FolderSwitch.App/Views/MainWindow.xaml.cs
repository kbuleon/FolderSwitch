// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Themes;
using FolderSwitch.Presentation.ViewModels;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.App.Views;

/// <summary>
/// The resident main window, bound to <see cref="MainViewModel"/>, hosting <see cref="MainView"/>. Built once; closing
/// it (Alt+F4) only hides it, through the view model, like ✕ does. Only <see cref="CloseForExit"/> really closes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Frame.</b> On Windows 11 the corners are rounded by the system: the small radius while the groups' own corners are
/// small (4 DIPs or less), the usual one otherwise, following every change of appearance. Windows 10 rounds nothing:
/// the window draws a 1 px border instead. Both get the shadow of a system frame (<see cref="WindowFrame"/>).
/// </para>
/// <para>
/// <b>Keyboard.</b> Tab and Shift+Tab show the next and previous tab, in a loop, and never move the focus between
/// controls (WPF's focus traversal is off, and nothing in the window takes the focus but a text box); Escape hides the
/// window. A text box that has the focus (a rename, in Customize mode) keeps its keys: Escape then cancels the rename. Escape first
/// closes the menu open in Customize mode, then the settings or About window, if any; F2 renames the selection. While
/// the settings or About window is open, Tab and F2 do nothing.
/// </para>
/// <para>
/// <b>Focus lost.</b> The menu open closes (not the settings or About window), and a shortcut capture in progress ends
/// (<see cref="MainView.OnWindowDeactivated"/>); whether the window hides is the controller's call.
/// </para>
/// <para>
/// <b>Text.</b> Laid out with layout rounding (crisp lines at every scale) and formatted in WPF's default « Ideal »
/// mode: « Display » mode, once the scale changed under existing text (the window moved to a monitor at 150 %),
/// drew ellipses on names that had room to spare in the off-screen renders.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(object),
        typeof(MainWindow),
        new PropertyMetadata(null, (d, _) => ((MainWindow)d).ApplyFrame()));

    /// <summary>Group corners at or under this radius make the window's corners small.</summary>
    private const int SmallCornersUpTo = 4;

    private bool _closingForExit;
    private nint _hwnd;
    private bool _shadowDone;
    private bool _cornersFailureLogged;

    public MainWindow(MainViewModel viewModel, ViewTexts texts, AppearanceViewModel appearance, SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(settings);

        SetResourceReference(AppearanceProperty, ThemeKeys.Appearance);
        InitializeComponent();
        DataContext = viewModel;
        View.Texts = texts;
        View.Appearance = appearance;
        View.Settings = settings;

        if (!WindowFrame.SupportsRoundedCorners)
        {
            BorderThickness = new Thickness(1);
        }
    }

    /// <summary>The appearance drawn (<see cref="AppearanceState"/>), from the resource <see cref="ThemeKeys.Appearance"/>.</summary>
    public object? Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>The content of the window: the menus and panels of Customize mode are its.</summary>
    internal MainView MainContent => View;

    /// <summary>Closes the window for good: only when FolderSwitch exits.</summary>
    public void CloseForExit()
    {
        _closingForExit = true;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        ApplyFrame();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        View.OnWindowMoved();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        View.OnWindowDeactivated();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closingForExit)
        {
            e.Cancel = true;
            (DataContext as MainViewModel)?.Close();
        }

        base.OnClosing(e);
    }

    /// <summary>Corners and shadow from the Desktop Window Manager, once the window has a handle.</summary>
    private void ApplyFrame()
    {
        if (_hwnd == 0)
        {
            return;
        }

        if (WindowFrame.SupportsRoundedCorners)
        {
            var small = Appearance is AppearanceState { RadiusPx: <= SmallCornersUpTo };
            if (!WindowFrame.SetRoundedCorners(_hwnd, small) && !_cornersFailureLogged)
            {
                _cornersFailureLogged = true;
                DebugLog.Warn("The window corners could not be rounded");
            }
        }

        if (!_shadowDone)
        {
            _shadowDone = true;
            if (!WindowFrame.EnableShadow(_hwnd))
            {
                DebugLog.Warn("The window shadow could not be enabled");
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // A column being resized stops (nothing is recorded); a rename's box cancels the rename itself; a shortcut
            // capture ends (the hook lets Escape through), an open drop-down list closes; otherwise an open menu, then
            // the settings or About window, closes before the window hides.
            if (Mouse.Captured is ColumnResizeGrip { IsResizing: true } grip)
            {
                grip.ReleaseMouseCapture();
                e.Handled = true;
            }
            else if (View.Settings?.Hotkey is { IsCapturing: true } hotkey)
            {
                hotkey.StopCapture();
                e.Handled = true;
            }
            else if (Mouse.Captured is ComboBox { IsDropDownOpen: true } list)
            {
                list.IsDropDownOpen = false;
                e.Handled = true;
            }
            else if (!InlineRename.IsRenameBox(Keyboard.FocusedElement) && (View.CloseFlyout() || View.CloseDialog()))
            {
                e.Handled = true;
            }

            return;
        }

        // The settings and About windows cover the board: its keys (another tab, a rename) wait until they close. A
        // text box of theirs (the custom color) keeps Tab; F2 never reaches the board.
        if (View.IsDialogOpen && (e.Key == Key.F2 || (e.Key == Key.Tab && Keyboard.FocusedElement is not TextBoxBase)))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Tab || Keyboard.FocusedElement is TextBoxBase || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0)
        {
            return;
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            viewModel.PreviousTab();
        }
        else
        {
            viewModel.NextTab();
        }

        e.Handled = true;
    }
}
