using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// Hosts a text with an ellipsis that layout rounding could cut short. At a scale such as 150 %, every desired width
/// is rounded to the nearest device pixel, sometimes under the text's own width; laid out at that width, the text then
/// shows an ellipsis with room to spare. This asks for one device pixel more than its text and gives the text all of
/// it. Given less room than the text needs, it passes the limit on, and the text is ellipsized as it should be.
/// </summary>
public sealed class TextRoom : Decorator
{
    /// <summary>One device pixel, in DIPs, at the scale <paramref name="element"/> is drawn at.</summary>
    public static double DevicePixel(Visual element) => 1 / VisualTreeHelper.GetDpi(element).DpiScaleX;

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is not { } child)
        {
            return default;
        }

        child.Measure(constraint);
        var desired = child.DesiredSize;
        return new Size(Math.Min(constraint.Width, desired.Width + DevicePixel(this)), desired.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(arrangeSize));
        return arrangeSize;
    }
}
