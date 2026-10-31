using System.Windows;
using System.Windows.Media;

namespace FolderSwitch.App.Views;

/// <summary>Walks of the element tree that also take the text elements a click can land on (a <c>Run</c>).</summary>
internal static class TreeHelpers
{
    /// <summary>True when <paramref name="node"/> is <paramref name="ancestor"/> or lies inside it.</summary>
    public static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);

        for (var current = node; current is not null; current = ParentOf(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The parent in the visual tree, else in the logical tree (a text element, the content of a popup).</summary>
    public static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element)
            : element is FrameworkContentElement content
                ? content.Parent ?? LogicalTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
}
