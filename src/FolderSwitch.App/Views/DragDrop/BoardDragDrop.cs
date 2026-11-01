using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views.DragDrop;

/// <summary>
/// The drag and drop of Customize mode: tabs within the tab bar; columns (by their grip), groups (by their grip) and
/// items to any place of their tab or of another one, which the tab bar shows when the drag rests on it (the view that
/// started the drag then calls <see cref="MainViewModel.EndDrag"/>). WPF's drag and drop, with the
/// view model being dragged as the data, in a format of FolderSwitch's own: nothing from another application is
/// accepted, and nothing dragged out of FolderSwitch means anything elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// The views start the drag (<see cref="Start"/>, from a <see cref="PressGesture"/> past the system's drag threshold)
/// and handle the drop on their own lists: the slot under the mouse (<see cref="SlotAt"/>, a gap between two elements)
/// is shown by an <see cref="InsertionAdorner"/> (<see cref="ShowIndicator"/>), and the drop hands the view model the
/// index the element must end at (<see cref="InsertionSlot"/>). A drop that would leave the element where it was
/// changes nothing.
/// </para>
/// <para>One drag at a time (WPF's drag is modal); UI thread only.</para>
/// </remarks>
internal static class BoardDragDrop
{
    /// <summary>The data format of a dragged tab, column, group or item: the view model itself.</summary>
    private const string Format = "FolderSwitch.BoardObject";

    /// <summary>What the dragged element fades to while it is dragged, as in the mockups.</summary>
    private const double DraggedOpacity = 0.4;

    /// <summary>How far the indicator stays from the sides of its list.</summary>
    private const double Inset = 3;

    private static InsertionAdorner? _indicator;
    private static AdornerLayer? _indicatorLayer;
    private static UIElement? _indicatorList;

    /// <summary>
    /// Drags <paramref name="payload"/>, shown by <paramref name="source"/> (faded meanwhile), until the button is
    /// released. Returns once the drag is over, dropped or not.
    /// </summary>
    public static void Start(FrameworkElement source, object payload)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(payload);

        var data = new DataObject(Format, payload);
        var opacity = source.Opacity;
        source.Opacity = DraggedOpacity;
        try
        {
            System.Windows.DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            // OLE can refuse a drag (another drag in progress, a COM failure): nothing moved.
            DebugLog.Warn("A drag and drop failed", e);
        }
        finally
        {
            source.Opacity = opacity;
            HideIndicator();
        }
    }

    /// <summary>The tab, column, group or item being dragged, if the drag is one of FolderSwitch's and carries a <typeparamref name="T"/>.</summary>
    public static T? Payload<T>(DragEventArgs e)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(e);
        return e.Data.GetDataPresent(Format) ? e.Data.GetData(Format) as T : null;
    }

    /// <summary>
    /// The slot of <paramref name="list"/> under <paramref name="position"/> (in the list's coordinates): the index of
    /// the first element whose middle lies past it, or the number of elements when none does. A horizontal list may wrap
    /// onto several lines (the tab bar): there, an element on a later line than the position lies past it, and one on an
    /// earlier line does not.
    /// </summary>
    public static int SlotAt(ItemsControl list, Point position, Orientation orientation)
    {
        ArgumentNullException.ThrowIfNull(list);

        var count = list.Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (BoundsOf(list, i) is not { } bounds)
            {
                continue;
            }

            var past = orientation == Orientation.Vertical
                ? position.Y < bounds.Top + (bounds.Height / 2)
                : bounds.Top > position.Y || (position.Y < bounds.Bottom && position.X < bounds.Left + (bounds.Width / 2));
            if (past)
            {
                return i;
            }
        }

        return count;
    }

    /// <summary>
    /// Shows the insertion indicator in <paramref name="slot"/> of <paramref name="list"/> (and nowhere else). In a
    /// horizontal list that wraps, a slot between the end of one line and the start of the next is shown on the line of
    /// <paramref name="pointer"/> (the mouse, in the list's coordinates), else at the end of the first one.
    /// </summary>
    public static void ShowIndicator(ItemsControl list, int slot, Orientation orientation, Point? pointer = null)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (!ReferenceEquals(_indicatorList, list))
        {
            HideIndicator();
            if (AdornerLayer.GetAdornerLayer(list) is not { } layer)
            {
                return;
            }

            _indicator = new InsertionAdorner(list, orientation);
            _indicatorLayer = layer;
            _indicatorList = list;
            layer.Add(_indicator);
        }

        if (orientation == Orientation.Vertical)
        {
            _indicator!.Place(LinePosition(list, slot, orientation, pointer, out _), Inset, Math.Max(Inset, list.ActualWidth - Inset));
            return;
        }

        // Across one line of tabs: the line of the elements around the slot.
        var position = LinePosition(list, slot, orientation, pointer, out var line);
        var top = line?.Top ?? 0;
        var bottom = line?.Bottom ?? list.ActualHeight;
        _indicator!.Place(position, top + Inset, Math.Max(top + Inset, bottom - Inset));
    }

    /// <summary>Removes the insertion indicator, wherever it is.</summary>
    public static void HideIndicator()
    {
        if (_indicator is not null)
        {
            _indicatorLayer?.Remove(_indicator);
        }

        _indicator = null;
        _indicatorLayer = null;
        _indicatorList = null;
    }

    /// <summary>
    /// A drag left <paramref name="target"/> or one of its parts: the indicator goes only when the mouse really is
    /// outside it (the leave of a part bubbles up while the mouse is still over the target).
    /// </summary>
    public static void HideIndicatorIfLeft(FrameworkElement target, DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(e);

        var position = e.GetPosition(target);
        if (position.X < 0 || position.Y < 0 || position.X > target.ActualWidth || position.Y > target.ActualHeight)
        {
            HideIndicator();
        }
    }

    /// <summary>
    /// Where the line of <paramref name="slot"/> runs: halfway across the gap between the two elements around it, just
    /// outside the first or the last element, or in the middle of an empty list. <paramref name="line"/> gives the bounds
    /// of an element of the line it runs along (a horizontal list may wrap: two elements on different lines are not
    /// both around the slot, the one on the line of <paramref name="pointer"/> is).
    /// </summary>
    private static double LinePosition(ItemsControl list, int slot, Orientation orientation, Point? pointer, out Rect? line)
    {
        var vertical = orientation == Orientation.Vertical;
        var count = list.Items.Count;
        var before = slot > 0 ? BoundsOf(list, Math.Min(slot, count) - 1) : null;
        var after = slot < count ? BoundsOf(list, slot) : null;

        if (!vertical && before is { } previous && after is { } next && Math.Abs(previous.Top - next.Top) >= 1)
        {
            if (pointer is { } p && p.Y >= next.Top)
            {
                before = null;
            }
            else
            {
                after = null;
            }
        }

        line = after ?? before;

        double Start(Rect r) => vertical ? r.Top : r.Left;
        double End(Rect r) => vertical ? r.Bottom : r.Right;

        return (before, after) switch
        {
            ({ } b, { } a) => (End(b) + Start(a)) / 2,
            (null, { } a) => Math.Max(0, Start(a) - 1),
            ({ } b, null) => End(b) + 1,
            _ => (vertical ? list.ActualHeight : list.ActualWidth) / 2,
        };
    }

    /// <summary>The bounds of element <paramref name="index"/> of <paramref name="list"/>, in the list's coordinates.</summary>
    private static Rect? BoundsOf(ItemsControl list, int index)
    {
        if (list.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container
            || !list.IsAncestorOf(container))
        {
            return null;
        }

        return container.TransformToAncestor(list).TransformBounds(new Rect(container.RenderSize));
    }
}
