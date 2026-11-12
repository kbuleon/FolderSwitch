// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FolderSwitch.App.Views.Layout;
using FolderSwitch.Core.Layout;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The resize grip of a column (bound to its <see cref="ColumnViewModel"/>), on its right edge, in Customize mode.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gesture.</b> The press captures the mouse; while it moves, the column is drawn at the width under the mouse
/// (<see cref="ColumnWidthPresenter.PreviewWidth"/>, which the column also shows as a number), stepped and bounded by
/// <see cref="ColumnWidths"/>; the release records it with one <see cref="MainViewModel.SetColumnWidth"/>, and so one
/// save. A release at the width the column started at records nothing. A capture
/// lost along the way (another window took the mouse) records nothing either. A double-click records the default
/// width (<see cref="MainViewModel.ResetColumnWidth"/>).
/// </para>
/// <para>
/// <b>Coordinates.</b> The width follows the mouse in screen coordinates: while the column grows, the window keeps its
/// horizontal center (spec 5.2) and so moves under the mouse, which a position taken from the column itself would
/// feed back into the width.
/// </para>
/// </remarks>
public partial class ColumnResizeGrip : UserControl
{
    private static readonly DependencyPropertyKey IsResizingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsResizing), typeof(bool), typeof(ColumnResizeGrip), new PropertyMetadata(false));

    public static readonly DependencyProperty IsResizingProperty = IsResizingPropertyKey.DependencyProperty;

    private int _startWidth;
    private double _startX;

    public ColumnResizeGrip()
    {
        InitializeComponent();
    }

    /// <summary>The column being resized: the presenter that lays out its groups.</summary>
    public ColumnWidthPresenter? Presenter { get; set; }

    /// <summary>True while the grip is dragged.</summary>
    public bool IsResizing => (bool)GetValue(IsResizingProperty);

    /// <summary>A width in DIPs as the column can have it: stepped, bounded.</summary>
    internal static int Snap(double width) =>
        Math.Clamp((int)Math.Round(width / ColumnWidths.Step) * ColumnWidths.Step, ColumnWidths.Min, ColumnWidths.Max);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        e.Handled = true;
        if (MainView.Of(this)?.DataContext is not MainViewModel { IsCustomizing: true } viewModel
            || DataContext is not ColumnViewModel column || Presenter is not { } presenter)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            Cancel();
            viewModel.ResetColumnWidth(column);
            return;
        }

        if (!CaptureMouse())
        {
            return;
        }

        _startWidth = Snap(presenter.EffectiveWidth);
        _startX = ScreenX(e);
        SetValue(IsResizingPropertyKey, true);
        presenter.PreviewWidth = _startWidth;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsResizing && Presenter is { } presenter)
        {
            presenter.PreviewWidth = Snap(_startWidth + (ScreenX(e) - _startX));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!IsResizing || Presenter is not { } presenter)
        {
            return;
        }

        e.Handled = true;
        var width = presenter.PreviewWidth;

        // Done before the capture goes, so that losing it does not cancel.
        SetValue(IsResizingPropertyKey, false);
        ReleaseMouseCapture();

        if (width is { } chosen && chosen != _startWidth
            && MainView.Of(this)?.DataContext is MainViewModel viewModel && DataContext is ColumnViewModel column)
        {
            viewModel.SetColumnWidth(column, chosen);
        }

        presenter.PreviewWidth = null;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        Cancel();
    }

    /// <summary>Ends a gesture without recording anything.</summary>
    private void Cancel()
    {
        if (!IsResizing)
        {
            return;
        }

        SetValue(IsResizingPropertyKey, false);
        if (Presenter is { } presenter)
        {
            presenter.PreviewWidth = null;
        }
    }

    /// <summary>The mouse's horizontal position on the screen, in DIPs.</summary>
    private double ScreenX(MouseEventArgs e)
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return PointToScreen(e.GetPosition(this)).X / scale;
    }
}
