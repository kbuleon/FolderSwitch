using System.Windows;

namespace FolderSwitch.App.Views;

/// <summary>
/// Whether a choice of a menu is the one in effect (a theme tile, a mode, a swatch): set by a binding on the button,
/// read by the triggers of its style (<c>Themes/Generic.xaml</c>).
/// </summary>
public static class Selection
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.RegisterAttached(
        "IsSelected",
        typeof(bool),
        typeof(Selection),
        new FrameworkPropertyMetadata(false));

    public static bool GetIsSelected(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsSelectedProperty);
    }

    public static void SetIsSelected(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsSelectedProperty, value);
    }
}
