using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FolderSwitch.Core.Layout;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// Gives its child (the groups of a column) the column's effective width: <see cref="PreviewWidth"/> while the column
/// is being resized, else <see cref="FixedWidth"/> when the column has one, otherwise the width its content asks for,
/// all bounded and stepped by <see cref="ColumnWidths.Effective"/>.
/// </summary>
/// <remarks>
/// <para>
/// The content is first measured without a width limit, which gives the width it needs (the longest group or item
/// name, see <see cref="ItemRowPanel"/>); that width is rounded to whole DIPs before <see cref="ColumnWidths.Auto"/>
/// rounds it up to the next step, so that floating-point noise in text widths (152.0000001) never adds a step. Then
/// the content is measured again at the effective width, which is what it is arranged at: every group of the column
/// shares it.
/// </para>
/// <para>
/// WPF re-measures a changed element with the width it was last given, here the column's: a name that grows is cut
/// to that width and its column never hears of it. So the texts that size a column (<see cref="FitText"/>), and the
/// views that show more or less in it (Customize mode, a rename), tell it themselves (<see cref="InvalidateColumnOf"/>,
/// <see cref="Remeasure"/>), and the column measures its content without a limit again.
/// </para>
/// </remarks>
public sealed class ColumnWidthPresenter : Decorator
{
    public static readonly DependencyProperty FixedWidthProperty = DependencyProperty.Register(
        nameof(FixedWidth),
        typeof(int?),
        typeof(ColumnWidthPresenter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty PreviewWidthProperty = DependencyProperty.Register(
        nameof(PreviewWidth),
        typeof(int?),
        typeof(ColumnWidthPresenter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private double _width = ColumnWidths.Min;

    /// <summary>The column's fixed width; <c>null</c> for an automatic width.</summary>
    public int? FixedWidth
    {
        get => (int?)GetValue(FixedWidthProperty);
        set => SetValue(FixedWidthProperty, value);
    }

    /// <summary>
    /// While the column's resize grip is dragged: the width under the mouse, drawn in place of the column's own until
    /// the gesture ends (<c>null</c> otherwise). Not recorded: the grip records the width once, at the end.
    /// </summary>
    public int? PreviewWidth
    {
        get => (int?)GetValue(PreviewWidthProperty);
        set => SetValue(PreviewWidthProperty, value);
    }

    /// <summary>The width the column has after the last measure.</summary>
    public double EffectiveWidth => _width;

    /// <summary>
    /// Something in <paramref name="element"/> that sizes its column changed (a text, a font, what is shown): the
    /// column that holds it, if any, measures its content again.
    /// </summary>
    public static void InvalidateColumnOf(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ColumnWidthPresenter column)
            {
                column.Remeasure();
                return;
            }
        }
    }

    /// <summary>
    /// Measures the column's content without a limit again, at the next layout. The elements between the column and
    /// the view that holds it are invalidated too: one still valid would answer its parent from its last measure and
    /// never reach the column, and the column measured alone afterwards (while being arranged, say) would not tell
    /// its parents its new width.
    /// </summary>
    public void Remeasure()
    {
        for (DependencyObject? current = this; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement element)
            {
                element.InvalidateMeasure();
            }

            if (current is UserControl)
            {
                return;
            }
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var child = Child;
        var fixedWidth = PreviewWidth ?? FixedWidth;
        var measured = 0.0;
        if (child is not null && fixedWidth is null)
        {
            child.Measure(new Size(double.PositiveInfinity, constraint.Height));
            measured = Math.Round(child.DesiredSize.Width);
        }

        _width = ColumnWidths.Effective(fixedWidth, measured);
        if (child is null)
        {
            return new Size(_width, 0);
        }

        child.Measure(new Size(_width, constraint.Height));
        return new Size(_width, child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, _width, arrangeSize.Height));
        return new Size(_width, arrangeSize.Height);
    }
}
