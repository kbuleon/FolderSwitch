using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Core.Layout;
using FolderSwitch.Presentation.ViewModels;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.App.Views;

/// <summary>
/// The first-run wizard's window, hosting <see cref="WizardView"/>. What happens when it closes is its owner's
/// business (<c>FirstRunWizard</c>); the window itself only shows the wizard, places itself, moves with its title row,
/// and ends a shortcut capture when it loses the focus or on Escape, so that a combination pressed elsewhere never
/// becomes the shortcut.
/// </summary>
public partial class WizardWindow : Window
{
    private readonly WizardViewModel _viewModel;

    /// <param name="windowsLanguage">The code of the language Windows uses, when FolderSwitch has it; <c>null</c> otherwise.</param>
    internal WizardWindow(WizardViewModel viewModel, ViewTexts texts, string? windowsLanguage)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(texts);

        _viewModel = viewModel;
        InitializeComponent();
        View.Attach(viewModel, texts, windowsLanguage);
        View.CloseRequested += OnCloseRequested;
        View.DragArea.MouseLeftButtonDown += OnDragAreaMouseDown;

        if (!WindowFrame.SupportsRoundedCorners)
        {
            BorderThickness = new Thickness(1);
        }
    }

    /// <summary>Lets go of the view model and of the texts, once the window is closed.</summary>
    internal void Release()
    {
        View.CloseRequested -= OnCloseRequested;
        View.DragArea.MouseLeftButtonDown -= OnDragAreaMouseDown;
        View.Dispose();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (WindowFrame.SupportsRoundedCorners && !WindowFrame.SetRoundedCorners(hwnd, small: false))
        {
            DebugLog.Warn("The corners of the wizard could not be rounded");
        }

        if (!WindowFrame.EnableShadow(hwnd))
        {
            DebugLog.Warn("The shadow of the wizard could not be enabled");
        }

        PlaceOnPrimaryMonitor(hwnd);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        _viewModel.Hotkey.StopCapture();
    }

    /// <summary>
    /// Centers the window on the primary monitor's work area, before it shows. It is first moved onto that monitor, so
    /// that it takes that monitor's scale (Windows rescales it if it was created on another); its size is then known in
    /// DIPs of that monitor, as the work area is. Without a monitor to read, WPF's own primary work area is used.
    /// </summary>
    private void PlaceOnPrimaryMonitor(nint hwnd)
    {
        if (DisplayMonitors.Primary() is not { } area)
        {
            DebugLog.Warn("The primary monitor could not be read: the wizard is centered on the work area known to WPF");
            var work = SystemParameters.WorkArea;
            var fallback = WindowPlacement.CenterOn(MeasureSize(), new RectD(work.X, work.Y, work.Width, work.Height));
            Left = fallback.X;
            Top = fallback.Y;
            return;
        }

        DisplayMonitors.MoveWindow(hwnd, area.Left, area.Top);
        var rect = WindowPlacement.CenterOn(MeasureSize(), area.ToDips());
        DisplayMonitors.MoveWindow(hwnd, (int)Math.Round(rect.X * area.Scale), (int)Math.Round(rect.Y * area.Scale));
    }

    /// <summary>The size the window takes for its content, in DIPs: the content's, plus the border.</summary>
    private SizeD MeasureSize()
    {
        View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var border = BorderThickness;
        return new SizeD(
            View.DesiredSize.Width + border.Left + border.Right,
            View.DesiredSize.Height + border.Top + border.Bottom);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>Escape ends a capture in progress, and nothing else: the wizard closes only with ✕ or Alt+F4.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.Hotkey.IsCapturing)
        {
            _viewModel.Hotkey.StopCapture();
            e.Handled = true;
        }
    }
}
