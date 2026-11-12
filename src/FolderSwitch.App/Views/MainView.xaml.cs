// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The content of the main window, bound to <see cref="MainViewModel"/>. The views inside reach the view model through
/// it: <see cref="ActivateItem"/> for a click on an item, the Open… methods for the windows and menus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Title bar.</b> Pressing the left button on it, anywhere but on its buttons, moves the window.
/// </para>
/// <para>
/// <b>Settings and About.</b> The ⚙ and (?) buttons of the title bar, in normal use as in Customize mode, open the
/// settings window (<see cref="SettingsView"/>) or the About window (<see cref="AboutView"/>), one at a time, in the
/// dialog layer: over the whole window, on a dimmed backdrop; the window grows to hold it when the board is smaller.
/// It closes with its ✕, a click on the backdrop, Escape (<see cref="CloseDialog"/>, called by the window) and when the
/// window hides; it stays open when the window only loses the focus (a file picker, another application). The settings
/// open again on the category shown last.
/// </para>
/// <para>
/// <b>Flyouts.</b> One at a time (<see cref="Flyout"/>): a group's « Add ▾ » menu (<see cref="AddMenu"/>) or palette,
/// in Customize mode; an item's menu (<see cref="ItemMenu"/>, a right-click), in normal use; a tab's, a group's or an
/// item's menu (<see cref="EditMenu"/>, a right-click), and the icon picker of a tab or a group (<see cref="IconPicker"/>,
/// whose filter box takes the keyboard focus), in Customize mode. A flyout closes on a click outside it, on
/// Escape (<see cref="CloseFlyout"/>, called by the window), when the window loses the focus to another window than the
/// flyout's own or hides, when Customize mode starts or ends, when another tab is shown, and when a window of the
/// dialog layer opens.
/// </para>
/// <para>
/// <b>Rename.</b> Nothing in the window takes the keyboard focus but a rename's text box (and, in its flyout, the icon
/// picker's filter box), so a click elsewhere would
/// leave it focused: such a click commits the rename first (<see cref="InlineRename.Commit"/>), then goes on to what
/// it was on.
/// </para>
/// </remarks>
public partial class MainView : UserControl
{
    public static readonly DependencyProperty TextsProperty = DependencyProperty.Register(
        nameof(Texts),
        typeof(ViewTexts),
        typeof(MainView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(AppearanceViewModel),
        typeof(MainView),
        new PropertyMetadata(null));

    /// <summary>The name of the icon button of a tab and of a group's header, where the icon picker opens.</summary>
    internal const string IconButtonName = "IconButton";

    private readonly Flyout _flyout;
    private MainViewModel? _viewModel;
    private SettingsPage _settingsPage = SettingsPage.General;

    public MainView()
    {
        InitializeComponent();
        AppIcon.Source = AppIconDrawing.Glyph;
        VersionRun.Text = SettingsView.Version;

        _flyout = new Flyout(FlyoutPopup);
        _flyout.Closed += OnFlyoutClosed;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;

        // The anchors of the title bar move with the window's width (a theme chosen in the appearance menu, say).
        SizeChanged += (_, _) => _flyout.Reposition();
    }

    /// <summary>The texts of the views themselves (tooltips, the settings and About windows).</summary>
    public ViewTexts? Texts
    {
        get => (ViewTexts?)GetValue(TextsProperty);
        set => SetValue(TextsProperty, value);
    }

    /// <summary>The view model of the settings window's appearance page (a dependency property: the title bar binds to it).</summary>
    public AppearanceViewModel? Appearance
    {
        get => (AppearanceViewModel?)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>The settings window's view model.</summary>
    public SettingsViewModel? Settings { get; set; }

    /// <summary>The pictures of the items: file type icons and favicons (<see cref="ItemIcon"/>); none, the drawn icons only.</summary>
    internal ItemIcons? Icons { get; set; }

    /// <summary>
    /// The author's Ko-fi page, which Ko-fi's button of the title bar opens (Customize mode), as the README's button.
    /// </summary>
    internal const string SupportPage = "https://ko-fi.com/karlbuleon";

    /// <summary>
    /// Opens the license and the third-party notices from the About window, and the Ko-fi page; none, these links do
    /// nothing.
    /// </summary>
    internal IShellLauncher? Shell { get; set; }

    /// <summary>
    /// True while the settings window shows its shortcut page: the shortcut then goes to its test zone instead of
    /// hiding the window (see the composition).
    /// </summary>
    internal bool IsHotkeyTestShown => DialogFrame.Child is SettingsView { Page: SettingsPage.Hotkey };

    /// <summary>True while the settings or the About window is open.</summary>
    internal bool IsDialogOpen => DialogFrame.Child is not null;

    /// <summary>The window open in the dialog layer; <c>null</c> when none is.</summary>
    internal FrameworkElement? OpenDialog => DialogFrame.Child as FrameworkElement;

    /// <summary>What the open flyout shows; <c>null</c> when none is open.</summary>
    internal FrameworkElement? OpenFlyoutContent => _flyout.Content;

    /// <summary>The snapshots: the layer the flyouts are drawn in, in place of a popup.</summary>
    internal Canvas? FlyoutLayer
    {
        get => _flyout.InlineLayer;
        set => _flyout.InlineLayer = value;
    }

    /// <summary>The <see cref="MainView"/> that holds <paramref name="element"/>, if any.</summary>
    internal static MainView? Of(DependencyObject? element)
    {
        for (var current = element; current is not null; current = TreeHelpers.ParentOf(current))
        {
            if (current is MainView view)
            {
                return view;
            }
        }

        return null;
    }

    /// <summary>
    /// A click (<paramref name="ctrl"/>: with Ctrl) on an item: carried out by the view model, which hides the window or
    /// says why it could not. Whatever goes wrong is logged, never thrown at the dispatcher.
    /// </summary>
    internal async void ActivateItem(ItemViewModel item, bool ctrl)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.ActivateItemAsync(item, ctrl);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Error($"Opening the item « {item.DisplayName} » failed", e);
        }
    }

    /// <summary>
    /// Opens the settings window on <paramref name="page"/>, or on the category shown last; shows that category when the
    /// window is open already.
    /// </summary>
    internal void OpenSettings(SettingsPage? page = null)
    {
        if (DialogFrame.Child is SettingsView open)
        {
            open.Show(page ?? open.Page);
            return;
        }

        if (Settings is { } settings && Appearance is { } appearance && Texts is { } texts)
        {
            ShowDialog(new SettingsView(settings, appearance, texts, page ?? _settingsPage, () => CloseDialog()));
        }
    }

    /// <summary>Opens the About window.</summary>
    internal void OpenAbout()
    {
        if (DialogFrame.Child is not AboutView && Texts is { } texts)
        {
            ShowDialog(new AboutView(texts, Shell, () => CloseDialog()));
        }
    }

    /// <summary>Closes the settings or the About window; false when none was open.</summary>
    internal bool CloseDialog()
    {
        if (DialogFrame.Child is not FrameworkElement content)
        {
            return false;
        }

        if (content is SettingsView settings)
        {
            _settingsPage = settings.Page;
        }

        DialogFrame.Child = null;
        DialogLayer.Visibility = Visibility.Collapsed;
        (content as IDisposable)?.Dispose();
        return true;
    }

    /// <summary>
    /// « Open in File Explorer », from an item's menu: carried out by the view model, which hides the window or says
    /// why it could not. Whatever goes wrong is logged, never thrown at the dispatcher.
    /// </summary>
    internal async void OpenInExplorer(ItemViewModel item)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.OpenInExplorerAsync(item);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Error($"Opening the item « {item.DisplayName} » in File Explorer failed", e);
        }
    }

    /// <summary>
    /// Opens the menu of <paramref name="item"/> under <paramref name="anchor"/> (a right-click, in normal use); a
    /// separator has none.
    /// </summary>
    internal void OpenItemMenu(ItemViewModel item, FrameworkElement anchor)
    {
        if (_viewModel is { IsCustomizing: false } viewModel && Texts is { } texts && !IsDialogOpen && !item.IsSeparator)
        {
            _flyout.Open(
                new ItemMenu(viewModel, item, texts, () => CloseFlyout(), OpenInExplorer, i => ActivateItem(i, ctrl: true)),
                anchor,
                FlyoutAlignment.Left);
        }
    }

    /// <summary>
    /// Opens the Edit-mode menu of a tab, a group or an item under <paramref name="anchor"/> (a right-click); its
    /// « Icon… » opens the icon picker under the icon button inside <paramref name="anchor"/>.
    /// </summary>
    internal void OpenEditMenu(RenamableViewModel target, FrameworkElement anchor)
    {
        if (_viewModel is { IsCustomizing: true } viewModel && Texts is { } texts && !IsDialogOpen)
        {
            _flyout.Open(
                new EditMenu(viewModel, target, texts, () => CloseFlyout(), t => OpenIconPicker(t, IconAnchorIn(anchor))),
                anchor,
                FlyoutAlignment.Left);
        }
    }

    /// <summary>
    /// Opens the icon picker of a tab or a group under <paramref name="anchor"/> (its icon button), in Customize mode; a
    /// click on the anchor of the open picker closes it instead. How long it took to show is logged.
    /// </summary>
    internal void OpenIconPicker(RenamableViewModel target, FrameworkElement anchor)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(anchor);

        if (_viewModel is not { IsCustomizing: true } viewModel || Texts is not { } texts || IsDialogOpen)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        if (viewModel.OpenIconPicker(target) is not { } picker)
        {
            return;
        }

        var view = new IconPicker(picker, texts, () => CloseFlyout());
        if (!_flyout.Open(view, anchor, FlyoutAlignment.Left))
        {
            return;
        }

        // Shown: laid out and drawn with its first category. Then the other categories, in the background.
        var shown = 0.0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => shown = clock.Elapsed.TotalMilliseconds);
        view.CompleteGrid(() => DebugLog.Info(FormattableString.Invariant(
            $"Icons: picker shown in {shown:F0} ms, its whole grid in {clock.Elapsed.TotalMilliseconds:F0} ms")));
    }

    /// <summary>The icon button inside <paramref name="element"/> (a tab, a group's header), shown; else the element itself.</summary>
    private static FrameworkElement IconAnchorIn(FrameworkElement element)
    {
        var pending = new Stack<DependencyObject>();
        pending.Push(element);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is FrameworkElement { Name: IconButtonName, IsVisible: true } button)
            {
                return button;
            }

            for (var i = VisualTreeHelper.GetChildrenCount(current) - 1; i >= 0; i--)
            {
                pending.Push(VisualTreeHelper.GetChild(current, i));
            }
        }

        return element;
    }

    /// <summary>
    /// Opens the « Add ▾ » menu of <paramref name="group"/> under <paramref name="anchor"/>, for the window FolderSwitch
    /// was called from; a click on the anchor of the open menu closes it instead. The menu's view model ends with it.
    /// </summary>
    internal void OpenAddMenu(GroupViewModel group, FrameworkElement anchor)
    {
        if (_viewModel is { IsCustomizing: true } viewModel && Texts is { } texts)
        {
            var menu = viewModel.OpenAddMenu(group);
            if (!_flyout.Open(new AddMenu(menu, viewModel, group, texts, () => CloseFlyout()), anchor, FlyoutAlignment.Left))
            {
                menu.Dispose();
            }
        }
    }

    /// <summary>Opens the palette of <paramref name="group"/> under its color dot <paramref name="anchor"/>.</summary>
    internal void OpenPalette(GroupViewModel group, FrameworkElement anchor)
    {
        if (_viewModel is { IsCustomizing: true } viewModel && Texts is { } texts)
        {
            _flyout.Open(new ColorPalettePopup(viewModel, group, texts, () => CloseFlyout()), anchor, FlyoutAlignment.Right);
        }
    }

    /// <summary>Closes the open flyout; false when none was open (Escape then hides the window).</summary>
    internal bool CloseFlyout() => _flyout.Close();

    /// <summary>The window moved: the open flyout follows its anchor.</summary>
    internal void OnWindowMoved() => _flyout.Reposition();

    /// <summary>
    /// The window lost the focus: a shortcut capture ends (the next keys pressed are meant for the other window, not
    /// for the capture) and the flyout closes, unless the focus went into the flyout itself (its popup is a window of
    /// its own). Checked once the focus has settled. The settings and About windows stay open.
    /// </summary>
    internal void OnWindowDeactivated()
    {
        Settings?.Hotkey.StopCapture();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (Window.GetWindow(this) is { IsActive: false } && _flyout.Content is not { IsKeyboardFocusWithin: true })
            {
                CloseFlyout();
            }
        });
    }

    /// <summary>Shows <paramref name="content"/> in the dialog layer, in place of the window open there and of any flyout.</summary>
    private void ShowDialog(FrameworkElement content)
    {
        CloseFlyout();
        CloseDialog();
        DialogFrame.Child = content;
        DialogLayer.Visibility = Visibility.Visible;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        CloseFlyout();
        CloseDialog();
        _viewModel = e.NewValue as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsCustomizing):
                // The menus of Edit mode end with it, and an item's menu is for normal use only.
                CloseFlyout();
                break;
            case nameof(MainViewModel.ActiveTab) when _flyout.Content is AddMenu or ColorPalettePopup or ItemMenu or EditMenu or IconPicker:
                // What a group's flyout acts on is no longer on screen.
                CloseFlyout();
                break;
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            CloseFlyout();
            CloseDialog();
        }
    }

    private void OnFlyoutClosed(object? sender, FrameworkElement content) => (content as IDisposable)?.Dispose();

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox box && InlineRename.IsRenameBox(box)
            && !TreeHelpers.IsWithin(e.OriginalSource as DependencyObject, box))
        {
            InlineRename.Commit(box);
        }
    }

    /// <summary>A click on the backdrop, outside the window it shows, closes that window.</summary>
    private void OnDialogLayerMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, DialogLayer))
        {
            CloseDialog();
            e.Handled = true;
        }
    }

    /// <summary>
    /// The left button pressed on the title bar, not on one of its buttons (they handle their own press): the window
    /// follows the mouse until the button is released (<see cref="Window.DragMove"/>, the system's move loop; the window
    /// keeps the focus). Only in <see cref="MainWindow"/>: the snapshots draw this view in no window. Where the window
    /// lands is the controller's (it is brought back inside the work area).
    /// </summary>
    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow window || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        e.Handled = true;
        try
        {
            window.DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was released before the move loop started: nothing to move.
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void OnAboutClick(object sender, RoutedEventArgs e) => OpenAbout();

    /// <summary>Opens the Ko-fi page in the default browser; a failure is the shell's to tell (<see cref="IShellLauncher.OpenUrl"/>).</summary>
    private void OnSupportClick(object sender, RoutedEventArgs e) => Shell?.OpenUrl(SupportPage);
}
