using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FolderSwitch.App.Themes;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>A color of the palette.</summary>
/// <param name="Hex">The color, <c>#rrggbb</c>.</param>
/// <param name="Name">Its name in the current language (<c>Color.&lt;key&gt;</c>).</param>
/// <param name="Brush">The color as drawn.</param>
/// <param name="IsCurrent">The group has this color.</param>
public sealed record PaletteSwatch(string Hex, string Name, Brush Brush, bool IsCurrent);

/// <summary>The palette of a group: a click on a color hands it to <see cref="MainViewModel.SetGroupColor"/>.</summary>
public partial class ColorPalettePopup : UserControl
{
    private readonly MainViewModel _main;
    private readonly GroupViewModel _group;
    private readonly Action _close;

    internal ColorPalettePopup(MainViewModel main, GroupViewModel group, ViewTexts texts, Action close)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        _main = main;
        _group = group;
        _close = close;
        Texts = texts;
        Swatches = ThemeCatalog.GroupPalette
            .Select(color => new PaletteSwatch(
                color.Hex,
                texts["Color." + color.Key],
                ThemeApplier.SolidBrush(color.Hex),
                string.Equals(color.Hex, group.Color, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        InitializeComponent();
    }

    public ViewTexts Texts { get; }

    public IReadOnlyList<PaletteSwatch> Swatches { get; }

    private void OnSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PaletteSwatch swatch })
        {
            _close();
            _main.SetGroupColor(_group, swatch.Hex);
        }
    }
}
