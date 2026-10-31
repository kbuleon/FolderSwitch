using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FolderSwitch.App.Themes;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.ViewModels;
using ThemeMode = FolderSwitch.Core.Model.ThemeMode;

namespace FolderSwitch.App.Views;

/// <summary>A mode of the appearance menu's segmented control.</summary>
public sealed record ModeChoice(ThemeMode Value, string Glyph, string Label, bool IsSelected);

/// <summary>A theme tile: its key, its name, and the background its thumbnail is drawn on (<c>null</c>: the theme's own).</summary>
public sealed record ThemeTile(string Key, string Label, string? Background, bool IsSelected);

/// <summary>A background swatch; <see cref="Hex"/> is <c>null</c> for the custom color.</summary>
public sealed record BackgroundSwatch(string? Hex, string Label, Brush Brush, bool IsSelected);

/// <summary>A choice of group corners, with the little box that shows it (dashed for « Theme »).</summary>
public sealed record RadiusChoice(GroupRadius Value, string Label, double BoxRadius, DoubleCollection? Dashes, bool IsSelected);

/// <summary>
/// The appearance menu, bound to <see cref="AppearanceViewModel"/>. Its lists are rebuilt whenever the view model or the
/// language changes, so that the thumbnails follow the variant, the swatches the theme and the names the language.
/// </summary>
/// <remarks>
/// <b>Custom color.</b> A full color picker would be a control of its own for one rarely used choice; a hex box with a
/// live preview gives any color (the view model keeps it for the theme and variant, and the text drawn on it stays
/// readable whatever it is). The box shows while the background is a custom one, or once the custom swatch is
/// clicked; Enter or leaving the box applies a valid color, anything else goes back to the current one.
/// </remarks>
public partial class AppearanceMenu : UserControl, IDisposable
{
    public static readonly DependencyProperty ModesProperty = DependencyProperty.Register(
        nameof(Modes), typeof(IReadOnlyList<ModeChoice>), typeof(AppearanceMenu));

    public static readonly DependencyProperty ThemesProperty = DependencyProperty.Register(
        nameof(Themes), typeof(IReadOnlyList<ThemeTile>), typeof(AppearanceMenu));

    public static readonly DependencyProperty BackgroundsProperty = DependencyProperty.Register(
        nameof(Backgrounds), typeof(IReadOnlyList<BackgroundSwatch>), typeof(AppearanceMenu));

    public static readonly DependencyProperty RadiiProperty = DependencyProperty.Register(
        nameof(Radii), typeof(IReadOnlyList<RadiusChoice>), typeof(AppearanceMenu));

    public static readonly DependencyProperty BackgroundNameProperty = DependencyProperty.Register(
        nameof(BackgroundName), typeof(string), typeof(AppearanceMenu), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsEditorShownProperty = DependencyProperty.Register(
        nameof(IsEditorShown), typeof(bool), typeof(AppearanceMenu), new PropertyMetadata(false));

    public static readonly DependencyProperty HexPreviewProperty = DependencyProperty.Register(
        nameof(HexPreview), typeof(Brush), typeof(AppearanceMenu));

    /// <summary>The little boxes of the corner choices are this much less round than the groups.</summary>
    private const double BoxRadiusScale = 1.8;

    private static readonly DoubleCollection ThemeDashes = Frozen([2, 1.5]);

    private readonly AppearanceViewModel _appearance;
    private bool _editorRequested;
    private bool _disposed;

    internal AppearanceMenu(AppearanceViewModel appearance, ViewTexts texts)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(texts);

        _appearance = appearance;
        Texts = texts;
        DataContext = appearance;
        InitializeComponent();

        Refresh();
        HexBox.Text = appearance.Background;
        appearance.PropertyChanged += OnAppearanceChanged;
        texts.PropertyChanged += OnTextsChanged;
    }

    public ViewTexts Texts { get; }

    public IReadOnlyList<ModeChoice>? Modes
    {
        get => (IReadOnlyList<ModeChoice>?)GetValue(ModesProperty);
        private set => SetValue(ModesProperty, value);
    }

    public IReadOnlyList<ThemeTile>? Themes
    {
        get => (IReadOnlyList<ThemeTile>?)GetValue(ThemesProperty);
        private set => SetValue(ThemesProperty, value);
    }

    public IReadOnlyList<BackgroundSwatch>? Backgrounds
    {
        get => (IReadOnlyList<BackgroundSwatch>?)GetValue(BackgroundsProperty);
        private set => SetValue(BackgroundsProperty, value);
    }

    public IReadOnlyList<RadiusChoice>? Radii
    {
        get => (IReadOnlyList<RadiusChoice>?)GetValue(RadiiProperty);
        private set => SetValue(RadiiProperty, value);
    }

    /// <summary>The name of the background in effect, under the swatches.</summary>
    public string BackgroundName
    {
        get => (string)GetValue(BackgroundNameProperty);
        private set => SetValue(BackgroundNameProperty, value);
    }

    /// <summary>Whether the custom color's box shows.</summary>
    public bool IsEditorShown
    {
        get => (bool)GetValue(IsEditorShownProperty);
        private set => SetValue(IsEditorShownProperty, value);
    }

    /// <summary>The color being typed, as it would look; transparent while it is not a color.</summary>
    public Brush? HexPreview
    {
        get => (Brush?)GetValue(HexPreviewProperty);
        private set => SetValue(HexPreviewProperty, value);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _appearance.PropertyChanged -= OnAppearanceChanged;
        Texts.PropertyChanged -= OnTextsChanged;
    }

    /// <summary>A color typed by the user as <c>#rrggbb</c> (the <c>#</c> may be left out); <c>null</c> when it is none.</summary>
    internal static string? ParseHex(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 6)
        {
            value = "#" + value;
        }

        return value.Length == 7 && ColorMath.IsValidHex(value) ? value.ToLowerInvariant() : null;
    }

    private static DoubleCollection Frozen(double[] values)
    {
        var collection = new DoubleCollection(values);
        collection.Freeze();
        return collection;
    }

    private void Refresh()
    {
        var vm = _appearance;
        Modes =
        [
            new ModeChoice(ThemeMode.Light, "☀", Texts["Appearance.Mode.Light"], vm.Mode == ThemeMode.Light),
            new ModeChoice(ThemeMode.Dark, "☾", Texts["Appearance.Mode.Dark"], vm.Mode == ThemeMode.Dark),
            new ModeChoice(ThemeMode.Auto, "◐", Texts["Appearance.Mode.Auto"], vm.Mode == ThemeMode.Auto),
        ];

        Themes = ThemeCatalog.ThemeKeys
            .Select(key => new ThemeTile(key, Texts["Theme." + key], key == vm.Theme ? vm.Background : null, key == vm.Theme))
            .ToList();

        var choices = vm.BackgroundChoices;
        var chosen = choices.FirstOrDefault(choice => string.Equals(choice.Hex, vm.Background, StringComparison.OrdinalIgnoreCase));
        var swatches = choices
            .Select(choice => new BackgroundSwatch(choice.Hex, BackgroundLabel(choice), ThemeApplier.SolidBrush(choice.Hex), ReferenceEquals(choice, chosen)))
            .ToList();
        swatches.Add(new BackgroundSwatch(null, Texts["Appearance.Background.Custom"], ThemePreview.CustomColorBrush, chosen is null));
        Backgrounds = swatches;
        BackgroundName = chosen is not null
            ? BackgroundLabel(chosen)
            : string.Format(CultureInfo.CurrentCulture, Texts["Appearance.Background.CustomValue"], vm.Background);
        IsEditorShown = _editorRequested || chosen is null;

        Radii =
        [
            Radius(GroupRadius.Theme, ThemeCatalog.RadiusPx(GroupRadius.Theme, vm.Theme), ThemeDashes),
            Radius(GroupRadius.Square, ThemeCatalog.RadiusPx(GroupRadius.Square, vm.Theme), null),
            Radius(GroupRadius.Slight, ThemeCatalog.RadiusPx(GroupRadius.Slight, vm.Theme), null),
            Radius(GroupRadius.Medium, ThemeCatalog.RadiusPx(GroupRadius.Medium, vm.Theme), null),
            Radius(GroupRadius.Strong, ThemeCatalog.RadiusPx(GroupRadius.Strong, vm.Theme), null),
        ];
    }

    private RadiusChoice Radius(GroupRadius value, int px, DoubleCollection? dashes) =>
        new(value, Texts["Radius." + value], px / BoxRadiusScale, dashes, value == _appearance.Radius);

    private string BackgroundLabel(NamedColor choice) =>
        choice.Key == AppearanceViewModel.DefaultBackgroundKey ? Texts["Appearance.Background.Default"] : Texts["Background." + choice.Key];

    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        Refresh();

        // The box shows the background in effect once it changed (a swatch, another theme): what was typed and not
        // applied must not be applied later, when the box loses the focus.
        if (e.PropertyName is null or "" or nameof(AppearanceViewModel.Background))
        {
            HexBox.Text = _appearance.Background;
        }
    }

    private void OnTextsChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ModeChoice choice })
        {
            _appearance.Mode = choice.Value;
        }
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThemeTile tile })
        {
            _appearance.Theme = tile.Key;
        }
    }

    private void OnBackgroundClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BackgroundSwatch swatch })
        {
            return;
        }

        if (swatch.Hex is { } hex)
        {
            _editorRequested = false;
            _appearance.Background = hex;
            HexBox.Text = _appearance.Background;
            Refresh();
            return;
        }

        // The custom color: its box, with the focus.
        _editorRequested = true;
        Refresh();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            HexBox.Focus();
            HexBox.SelectAll();
        });
    }

    private void OnRadiusClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RadiusChoice choice })
        {
            _appearance.Radius = choice.Value;
        }
    }

    private void OnHexChanged(object sender, TextChangedEventArgs e) =>
        HexPreview = ParseHex(HexBox.Text) is { } hex ? ThemeApplier.SolidBrush(hex) : Brushes.Transparent;

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyHex();
            HexBox.SelectAll();
        }
    }

    private void OnHexLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyHex();

    /// <summary>
    /// Applies a valid color typed in the box. Not once the menu is closed: the box then loses the focus because its
    /// window closed (Escape), which applies nothing.
    /// </summary>
    private void ApplyHex()
    {
        if (_disposed)
        {
            return;
        }

        if (ParseHex(HexBox.Text) is { } hex)
        {
            _appearance.Background = hex;
        }

        HexBox.Text = _appearance.Background;
    }
}
