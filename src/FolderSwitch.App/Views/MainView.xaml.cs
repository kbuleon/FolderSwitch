using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The content of the main window, bound to <see cref="MainViewModel"/>. The views inside reach the view model through
/// it: <see cref="ActivateItem"/> for a click on an item, the Open… methods for the menus and panels of Customize mode.
/// </summary>
/// <remarks>
/// <para>
/// <b>Flyouts.</b> One at a time (<see cref="Flyout"/>): the appearance menu, the settings panel, a group's
/// « + Folder ▾ » menu or palette. A flyout closes on a click outside it, on Escape (<see cref="CloseFlyout"/>, called
/// by the window), when the window loses the focus to another window than the flyout's own or hides, when Customize
/// mode ends, and, for those of a group, when another tab is shown. Closing the settings panel ends a shortcut capture
/// in progress.
/// </para>
/// <para>
/// <b>Rename.</b> Nothing in the window takes the keyboard focus but a rename's text box, so a click elsewhere would
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
        new PropertyMetadata(null, OnTextsChanged));

    private static readonly DependencyPropertyKey AppearanceLabelPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(AppearanceLabel),
        typeof(string),
        typeof(MainView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AppearanceLabelProperty = AppearanceLabelPropertyKey.DependencyProperty;

    private readonly Flyout _flyout;
    private AppearanceViewModel? _appearance;
    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();
        AppIcon.Source = AppIconDrawing.Glyph;

        _flyout = new Flyout(FlyoutPopup);
        _flyout.Closed += OnFlyoutClosed;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;

        // The anchors of the title bar move with the window's width (a theme chosen in the appearance menu, say).
        SizeChanged += (_, _) => _flyout.Reposition();
    }

    /// <summary>The texts of the views themselves (tooltips, the appearance button).</summary>
    public ViewTexts? Texts
    {
        get => (ViewTexts?)GetValue(TextsProperty);
        set => SetValue(TextsProperty, value);
    }

    /// <summary>The label of the appearance button: « Fluent · Light », in the current language.</summary>
    public string AppearanceLabel => (string)GetValue(AppearanceLabelProperty);

    /// <summary>The appearance menu's view model.</summary>
    public AppearanceViewModel? Appearance
    {
        get => _appearance;
        set
        {
            if (_appearance is not null)
            {
                _appearance.PropertyChanged -= OnAppearancePropertyChanged;
            }

            _appearance = value;
            if (value is not null)
            {
                value.PropertyChanged += OnAppearancePropertyChanged;
            }

            UpdateAppearanceLabel();
        }
    }

    /// <summary>The settings panel's view model.</summary>
    public SettingsViewModel? Settings { get; set; }

    /// <summary>
    /// True while the settings panel is open: the shortcut then goes to its test zone instead of hiding the window
    /// (see the composition).
    /// </summary>
    internal bool IsSettingsPanelOpen => _flyout.Content is SettingsPanel;

    /// <summary>What the open flyout shows; <c>null</c> when none is open.</summary>
    internal FrameworkElement? OpenFlyoutContent => _flyout.Content;

    /// <summary>The snapshots: the layer the flyouts are drawn in, in place of a popup.</summary>
    internal Canvas? FlyoutLayer
    {
        get => _flyout.InlineLayer;
        set => _flyout.InlineLayer = value;
    }

    /// <summary>The ⚙ button, the settings panel's anchor.</summary>
    internal FrameworkElement SettingsAnchor => SettingsButton;

    /// <summary>The « ◐ Theme · Mode » button, the appearance menu's anchor.</summary>
    internal FrameworkElement AppearanceAnchor => AppearanceButton;

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

    /// <summary>Opens the appearance menu under its button (Customize mode).</summary>
    internal void OpenAppearanceMenu()
    {
        if (_viewModel is { IsCustomizing: true } && Appearance is { } appearance && Texts is { } texts)
        {
            _flyout.Open(new AppearanceMenu(appearance, texts), AppearanceButton, FlyoutAlignment.Right);
        }
    }

    /// <summary>Opens the settings panel under the ⚙ button (Customize mode), its shortcut test zone waiting for a press.</summary>
    internal void OpenSettingsPanel()
    {
        if (_viewModel is { IsCustomizing: true } && Settings is { } settings && Texts is { } texts)
        {
            settings.Hotkey.ResetTest();
            _flyout.Open(new SettingsPanel(settings, texts), SettingsButton, FlyoutAlignment.Right);
        }
    }

    /// <summary>Opens the « + Folder ▾ » menu of <paramref name="group"/> under <paramref name="anchor"/>.</summary>
    internal void OpenAddFolderMenu(GroupViewModel group, FrameworkElement anchor)
    {
        if (_viewModel is { IsCustomizing: true } viewModel && Texts is { } texts)
        {
            _flyout.Open(new AddFolderMenu(viewModel, group, texts, () => CloseFlyout()), anchor, FlyoutAlignment.Left);
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
    /// for the capture) and the flyout closes, unless the focus went into the
    /// flyout itself: its popup is a window of its own, which a text box taking the keyboard (the custom color of the
    /// appearance menu) activates. Checked once the focus has settled.
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

    private static void OnTextsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MainView)d;
        if (e.OldValue is ViewTexts old)
        {
            old.PropertyChanged -= view.OnLanguageChanged;
        }

        if (e.NewValue is ViewTexts texts)
        {
            texts.PropertyChanged += view.OnLanguageChanged;
        }

        view.UpdateAppearanceLabel();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => UpdateAppearanceLabel();

    private void OnAppearancePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(AppearanceViewModel.Theme) or nameof(AppearanceViewModel.Mode))
        {
            UpdateAppearanceLabel();
        }
    }

    /// <summary>« Fluent · Light »: the theme's and the mode's names (spec 5.4: « ◐ Theme · Mode »).</summary>
    private void UpdateAppearanceLabel()
    {
        var label = Appearance is { } appearance && Texts is { } texts
            ? $"{texts["Theme." + appearance.Theme]} · {texts["Appearance.Mode." + appearance.Mode]}"
            : string.Empty;
        SetValue(AppearanceLabelPropertyKey, label);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        CloseFlyout();
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
            case nameof(MainViewModel.IsCustomizing) when _viewModel is { IsCustomizing: false }:
                CloseFlyout();
                break;
            case nameof(MainViewModel.ActiveTab) when _flyout.Content is AddFolderMenu or ColorPalettePopup:
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
        }
    }

    private void OnFlyoutClosed(object? sender, FrameworkElement content)
    {
        if (content is SettingsPanel)
        {
            Settings?.Hotkey.StopCapture();
        }

        (content as IDisposable)?.Dispose();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox box && InlineRename.IsRenameBox(box)
            && !TreeHelpers.IsWithin(e.OriginalSource as DependencyObject, box))
        {
            InlineRename.Commit(box);
        }
    }

    private void OnAppearanceClick(object sender, RoutedEventArgs e) => OpenAppearanceMenu();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettingsPanel();
}
