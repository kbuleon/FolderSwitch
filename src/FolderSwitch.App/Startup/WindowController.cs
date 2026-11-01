using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Services;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Layout;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.Services;
using FolderSwitch.Presentation.ViewModels;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.App.Startup;

/// <summary>
/// Shows and hides the resident main window, which is built once and never closed.
/// <para>
/// <b>Show</b> (the shortcut while the window is hidden, see <see cref="ShortcutRouting"/>; <see cref="Open"/> on a
/// click on the notification icon, its « Open » entry or a second launch; <see cref="ShowCustomize"/>): the calling
/// window is captured first, before FolderSwitch takes the focus; the view model is told
/// (<see cref="MainViewModel.OnShown"/>, which may change the tab shown, hence the size); the window is centered on the
/// monitor that holds the mouse cursor (the primary monitor if that cannot be read); then shown, activated and brought
/// to the foreground. The time from the trigger to the first frame is written to the debug log. The window follows the
/// size of its content within that monitor's work area (beyond it the board scrolls); whenever that size changes while
/// it is shown (a group collapsed, another tab), it keeps its top edge and its horizontal center (spec 5.2,
/// <see cref="WindowPlacement.Reanchor"/>).
/// </para>
/// <para>
/// <b>Recall</b> (<see cref="Recall"/>, the shortcut while the window is on screen behind another window, which only
/// Customize mode allows): the calling window is captured again and handed to the view model
/// (<see cref="MainViewModel.OnRecalled"/>, which keeps the mode and the tab), and the window comes to the front where
/// it is.
/// </para>
/// <para>
/// <b>Hide</b> (<see cref="HideWindow"/>, from the view model: Escape, ✕, a done activation, the loss of the focus in
/// normal use; or the shortcut while the window is in front). After an item opened in the calling Explorer window or file
/// dialog (see <see cref="ReturnFocusAfterHide"/>), that window is brought back to the foreground, since Windows does not
/// always give it the focus back by itself.
/// </para>
/// </summary>
/// <remarks>UI thread only.</remarks>
internal sealed class WindowController : IWindowHost
{
    /// <summary>Far outside every monitor: where the window is rendered once at startup, unseen.</summary>
    private const double OffScreen = -32000;

    private readonly ICallContextProvider _contexts;
    private readonly IForegroundActivator _foreground;
    private readonly UserPrompts _prompts;

    private Window? _window;
    private MainViewModel? _viewModel;
    private nint _hwnd;
    private bool _prerendering;
    private int _showCount;
    private nint _returnFocusTo;
    private MonitorWorkArea? _placedOn;

    /// <summary>Where the window was last placed, in DIPs of <see cref="_placedOn"/>.</summary>
    private RectD? _placedRect;

    private bool _reanchoring;

    public WindowController(ICallContextProvider contexts, IForegroundActivator foreground, UserPrompts prompts)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(prompts);

        _contexts = contexts;
        _foreground = foreground;
        _prompts = prompts;
    }

    /// <summary>Whether the window is on screen (the off-screen render at startup does not count).</summary>
    public bool IsShown => _window is { IsVisible: true } && !_prerendering;

    /// <summary>
    /// Whether FolderSwitch is in front: the window is shown and the foreground window is one of the process's own (the
    /// main window, or a menu or panel of it).
    /// </summary>
    public bool IsInFront => IsShown && (_window!.IsActive || _foreground.IsOwnProcessInForeground());

    /// <summary>How many times the window was shown: identifies the current call, as the view model's own count does.</summary>
    public int ShowCount => _showCount;

    /// <summary>Takes charge of the main window and its view model; once, before anything is shown.</summary>
    public void Attach(Window window, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(viewModel);
        if (_window is not null)
        {
            throw new InvalidOperationException("The main window is already attached.");
        }

        _window = window;
        _viewModel = viewModel;
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        window.Deactivated += OnDeactivated;
        window.DpiChanged += OnDpiChanged;
        window.SizeChanged += OnSizeChanged;
    }

    /// <summary>Stops reacting to the window, before it is closed at exit.</summary>
    public void Detach()
    {
        if (_window is null)
        {
            return;
        }

        _window.Deactivated -= OnDeactivated;
        _window.DpiChanged -= OnDpiChanged;
        _window.SizeChanged -= OnSizeChanged;
    }

    /// <summary>
    /// Shows the window once, off-screen and without activating it, so that its first layout and render happen at
    /// startup instead of at the first call; then hides it and calls <paramref name="onReady"/>. Never blocks: the
    /// render is awaited through the dispatcher.
    /// </summary>
    public void Prerender(Action onReady)
    {
        ArgumentNullException.ThrowIfNull(onReady);
        var window = RequireWindow();

        _prerendering = true;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = OffScreen;
        window.Top = OffScreen;
        window.ShowActivated = false;

        void OnRendered(object? sender, EventArgs e)
        {
            window.ContentRendered -= OnRendered;
            window.ShowActivated = true;

            // A call that came before the first frame took the window over (see ShowWindow): it stays shown.
            if (_prerendering)
            {
                _prerendering = false;
                window.Hide();
            }

            onReady();
        }

        window.ContentRendered += OnRendered;
        window.Show();
    }

    /// <summary>Whether one of FolderSwitch's own boxes or pickers is open.</summary>
    public bool IsPromptOpen => _prompts.IsOpen;

    /// <summary>
    /// The shortcut while the window is on screen but behind another window (<see cref="ShortcutAction.Recall"/>): that
    /// window, now in the foreground, is captured as the calling window, the view model keeps its mode and tab
    /// (<see cref="MainViewModel.OnRecalled"/>), and the window comes to the front, where it is. A hidden window is shown
    /// instead.
    /// </summary>
    public void Recall()
    {
        if (!IsShown)
        {
            ShowWindow();
            return;
        }

        // First, while the calling window still has the focus.
        var ctx = _contexts.CaptureForeground();
        _showCount++;
        _returnFocusTo = 0;
        _viewModel!.OnRecalled(ctx);
        BringToFront();
        DebugLog.Info($"Recalled to the front (context {ctx.Kind})");
    }

    /// <summary>
    /// A click on the notification icon, its « Open » entry, a second launch (spec 5.9), or the shortcut while the window
    /// is hidden: shows the window, or brings it to the foreground if already shown. Never hides it.
    /// </summary>
    public void Open()
    {
        if (IsShown)
        {
            BringToFront();
        }
        else
        {
            ShowWindow();
        }
    }

    /// <summary>
    /// « Customize » in the notification menu: shows the window (or brings it to the foreground if already shown) and
    /// enters Customize mode.
    /// </summary>
    public void ShowCustomize()
    {
        Open();

        if (_viewModel is { IsCustomizing: false } viewModel)
        {
            viewModel.ToggleCustomize();
        }
    }

    /// <inheritdoc/>
    public void HideWindow()
    {
        if (_window is not { IsVisible: true } window || _prerendering)
        {
            return;
        }

        var returnTo = _returnFocusTo;
        _returnFocusTo = 0;
        window.Hide();

        // Every hide of the shown window goes through here (the view model's, the shortcut's).
        _viewModel?.OnHidden();

        if (returnTo != 0)
        {
            _foreground.Activate(returnTo);
        }
    }

    /// <summary>
    /// An item was opened in the calling window <paramref name="ctx"/> (see <see cref="FocusReturningActivator"/>): the
    /// hide that follows brings that window back to the foreground. Only for an Explorer window or a file dialog, and
    /// only while the window is still shown for the call the activation started in (<paramref name="showCount"/>, the
    /// <see cref="ShowCount"/> of then): the view model drops a later outcome without hiding, so it must not be left
    /// to the next hide (one on a loss of focus would take the focus back). Any other show or hide forgets it.
    /// </summary>
    public void ReturnFocusAfterHide(CallContext ctx, int showCount)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (IsShown && showCount == _showCount && ctx.Window != 0
            && ctx.Kind is CallContextKind.Explorer or CallContextKind.FileDialog)
        {
            _returnFocusTo = ctx.Window;
        }
    }

    private void ShowWindow()
    {
        var window = RequireWindow();
        var viewModel = _viewModel!;
        var start = Stopwatch.GetTimestamp();

        // Called before the startup render finished: the window is already up, off-screen; it is simply placed.
        _prerendering = false;
        window.ShowActivated = true;

        // First, while the calling window still has the focus.
        var ctx = _contexts.CaptureForeground();
        var captured = Stopwatch.GetTimestamp();

        _showCount++;
        _returnFocusTo = 0;
        viewModel.OnShown(ctx);

        // The tab shown may have changed: the window is sized to its content now, within the monitor it goes to.
        PlaceOnCursorMonitor(window);

        window.Show();
        window.Activate();
        _foreground.Activate(_hwnd);

        MeasureFirstFrame(start, captured, ctx.Kind);
    }

    /// <summary>
    /// Centers the window on the work area of the monitor under the mouse cursor, or of the primary monitor when the
    /// cursor's cannot be read, or at worst of WPF's own primary work area: the window must never stay where it was
    /// rendered at startup, off-screen. It is first moved onto that monitor: if its DPI differs, Windows rescales the
    /// window now, so that its size in DIPs and the monitor's scale agree for the centering. Its size is then bounded by
    /// that monitor's work area (beyond it, the board scrolls) and brought up to date with its content. Coordinates are
    /// DIPs of that monitor (its pixels divided by its scale).
    /// </summary>
    private void PlaceOnCursorMonitor(Window window)
    {
        var area = DisplayMonitors.AtCursor() ?? DisplayMonitors.Primary() ?? SystemWorkArea(window);
        _placedOn = area;
        DisplayMonitors.MoveWindow(_hwnd, area.Left, area.Top);

        // The largest size the work area allows: SizeToContent stays within it.
        var bounds = WindowPlacement.ClampToWorkArea(new SizeD(double.PositiveInfinity, double.PositiveInfinity), area.ToDips());
        window.MaxWidth = bounds.Width;
        window.MaxHeight = bounds.Height;
        window.UpdateLayout();

        Center(window, area);
    }

    /// <summary>The primary work area as WPF knows it (DIPs at the window's DPI), in pixels.</summary>
    private static MonitorWorkArea SystemWorkArea(Window window)
    {
        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        var work = SystemParameters.WorkArea;
        DebugLog.Warn("No monitor could be read: the window is centered on the primary work area known to WPF");
        return new MonitorWorkArea(
            (int)Math.Round(work.X * scale),
            (int)Math.Round(work.Y * scale),
            (int)Math.Round(work.Width * scale),
            (int)Math.Round(work.Height * scale),
            scale);
    }

    private void BringToFront()
    {
        RequireWindow().Activate();
        _foreground.Activate(_hwnd);
    }

    private void Center(Window window, MonitorWorkArea area)
    {
        var size = new SizeD(window.ActualWidth, window.ActualHeight);
        MoveTo(WindowPlacement.CenterOn(size, area.ToDips()), area);
    }

    /// <summary>Moves the window (never resizes it) to <paramref name="rect"/>, in DIPs of <paramref name="area"/>.</summary>
    private void MoveTo(RectD rect, MonitorWorkArea area)
    {
        DisplayMonitors.MoveWindow(_hwnd, (int)Math.Round(rect.X * area.Scale), (int)Math.Round(rect.Y * area.Scale));
        _placedRect = rect;
    }

    /// <summary>
    /// The content changed size while the window is shown (a group collapsed or expanded, another tab, an addition):
    /// SizeToContent has resized the window from its top-left corner; it is moved back so that its top edge and its
    /// horizontal center stay where they were (spec 5.2), within the work area. Only a move, never a resize: it cannot
    /// cause another size change, so there is no loop.
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsShown || _reanchoring || _placedOn is not { } area || _placedRect is not { } current)
        {
            return;
        }

        _reanchoring = true;
        try
        {
            var rect = WindowPlacement.Reanchor(current, new SizeD(e.NewSize.Width, e.NewSize.Height), area.ToDips());
            MoveTo(rect, area);
        }
        finally
        {
            _reanchoring = false;
        }
    }

    /// <summary>
    /// A hidden window may only take the DPI of its monitor once shown: Windows then rescales it, and moves it as it
    /// sees fit. It is centered again on the same monitor once that is done.
    /// </summary>
    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        if (!IsShown || _placedOn is not { } area || _window is not { } window)
        {
            return;
        }

        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (IsShown)
            {
                window.UpdateLayout();
                Center(window, area);
            }
        });
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // A box or picker of FolderSwitch's own took the focus: the window stays (the view model checks its own
        // prompts too; this also covers the messages that do not come from it).
        if (!IsShown || _prompts.IsOpen)
        {
            return;
        }

        _viewModel?.OnDeactivated();
    }

    private static void MeasureFirstFrame(long start, long captured, CallContextKind kind)
    {
        void OnRendering(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            var total = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var capture = Stopwatch.GetElapsedTime(start, captured).TotalMilliseconds;
            DebugLog.Info(FormattableString.Invariant(
                $"Shown: first frame {total:F1} ms after the trigger (context {kind}, captured in {capture:F1} ms)"));
        }

        CompositionTarget.Rendering += OnRendering;
    }

    private Window RequireWindow() =>
        _window ?? throw new InvalidOperationException("The main window is not attached yet.");
}
