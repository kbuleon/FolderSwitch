// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

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
/// <b>Show</b> (<see cref="Open"/>: the shortcut while the window is hidden, see <see cref="ShortcutRouting"/>; a click
/// on the notification icon, its « Open » entry or a second launch; <see cref="ShowCustomize"/>): the calling window is
/// captured first, before FolderSwitch takes the focus; the view model is told (<see cref="MainViewModel.OnShown"/>,
/// which may change the tab shown, hence the size); the window is placed on the monitor that holds the mouse cursor,
/// centered on its work area, whatever showed the window (centered on the primary monitor if the cursor cannot be
/// read); then shown, activated and brought to
/// the foreground. The time from the trigger to the first frame is written to the debug log. Nothing of a previous
/// placement is kept. The window follows the size of its content within that monitor's work area (beyond it the board
/// scrolls); whenever that size changes while it is shown (a group collapsed, another tab), it keeps its top edge and
/// its horizontal center (spec 5.2, <see cref="WindowPlacement.Reanchor"/>).
/// </para>
/// <para>
/// <b>Move</b> (the title bar dragged, Windows' move loop): while it lasts, the window is left alone, even when Windows
/// rescales it for a monitor with another DPI. Once dropped, it slides back just enough to be fully inside the work area
/// of the monitor holding it (<see cref="WindowPlacement.FitInside"/>), which it now belongs to: a size change keeps its
/// top edge and horizontal center there, and a DPI change keeps it where it is instead of centering it again.
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

    /// <summary>The window enters Windows' move (or size) loop: a drag of the title bar starts.</summary>
    private const int WmEnterSizeMove = 0x0231;

    /// <summary>The window leaves Windows' move (or size) loop: the drag ends.</summary>
    private const int WmExitSizeMove = 0x0232;

    private readonly ICallContextProvider _contexts;
    private readonly IForegroundActivator _foreground;
    private readonly UserPrompts _prompts;

    private Window? _window;
    private MainViewModel? _viewModel;
    private nint _hwnd;
    private HwndSource? _source;
    private bool _prerendering;
    private int _showCount;
    private nint _returnFocusTo;
    private MonitorWorkArea? _placedOn;

    /// <summary>Where the window was last placed, in DIPs of <see cref="_placedOn"/>.</summary>
    private RectD? _placedRect;

    /// <summary>How the window was placed last: applied again when its DPI changes.</summary>
    private Anchor _anchor;

    private bool _reanchoring;

    /// <summary>True from the start of a drag of the window to the moment its drop is handled.</summary>
    private bool _moving;

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
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(OnWindowMessage);
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
        _source?.RemoveHook(OnWindowMessage);
        _source = null;
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

    /// <summary>Whether one of FolderSwitch's own boxes is open (not a Windows picker), see <see cref="UserPrompts.OpenBox"/>.</summary>
    public bool IsBoxOpen => _prompts.OpenBox is not null;

    /// <summary>
    /// The shortcut while one of FolderSwitch's own boxes is open (<see cref="ShortcutAction.FrontBox"/>): the box comes
    /// to the front and takes the keyboard, as the main window does when it is shown. The press of the shortcut is what
    /// lets Windows give FolderSwitch the foreground, even for a box shown without the focus (the update offer) or left
    /// behind another application with the main window it belongs to.
    /// </summary>
    public void BringBoxToFront()
    {
        if (_prompts.OpenBox is not { } box)
        {
            return;
        }

        box.Activate();
        _foreground.Activate(new WindowInteropHelper(box).Handle);
        DebugLog.Info("Shortcut: the open box is brought to the front");
    }

    /// <summary>
    /// The shortcut while the window is on screen but behind another window (<see cref="ShortcutAction.Recall"/>): that
    /// window, now in the foreground, is captured as the calling window, the view model keeps its mode and tab
    /// (<see cref="MainViewModel.OnRecalled"/>), and the window comes to the front, where it is. A hidden window is shown
    /// instead, as <see cref="Open"/> does.
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
    /// is hidden: shows the window, or brings it to the foreground, where it is, if already shown. Never hides it.
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
    /// « Customize » in the notification menu: shows the window (or brings it to the foreground if
    /// already shown) and enters Customize mode.
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
    /// Places the window on the work area of the monitor under the mouse cursor;
    /// when the cursor cannot be read, centers it on the primary monitor's, or at worst on WPF's own primary work area:
    /// the window must never stay where it was rendered at startup, off-screen. It is first moved onto that monitor: if
    /// its DPI differs, Windows rescales the window now, so that its size in DIPs and the monitor's scale agree for the
    /// placement. Its size is then bounded by that monitor's work area (beyond it, the board scrolls) and brought up to
    /// date with its content. Coordinates are DIPs of that monitor (its pixels divided by its scale).
    /// </summary>
    private void PlaceOnCursorMonitor(Window window)
    {
        var cursor = DisplayMonitors.Cursor();
        var area = cursor?.WorkArea ?? DisplayMonitors.Primary() ?? SystemWorkArea(window);
        _anchor = Anchor.Screen;
        _placedOn = area;

        // A hidden window is in no drag: a drop left unhandled must not hold the window still from now on.
        _moving = false;
        DisplayMonitors.MoveWindow(_hwnd, area.Left, area.Top);
        BoundTo(window, area);
        window.UpdateLayout();

        Place(window, area);
    }

    /// <summary>
    /// The largest size the work area allows: SizeToContent stays within it, and the board scrolls beyond. In DIPs of
    /// <paramref name="area"/>.
    /// </summary>
    private static void BoundTo(Window window, MonitorWorkArea area)
    {
        var bounds = WindowPlacement.ClampToWorkArea(new SizeD(double.PositiveInfinity, double.PositiveInfinity), area.ToDips());
        window.MaxWidth = bounds.Width;
        window.MaxHeight = bounds.Height;
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

    /// <summary>
    /// Centers the window on the work area of <paramref name="area"/>, fully inside it.
    /// </summary>
    private void Place(Window window, MonitorWorkArea area)
    {
        var size = new SizeD(window.ActualWidth, window.ActualHeight);
        MoveTo(WindowPlacement.CenterOn(size, area.ToDips()), area);
    }

    /// <summary>
    /// Leaves the window where it is, only brought fully inside the work area of the monitor holding it (the largest part
    /// of it), which it now belongs to (<see cref="Anchor.User"/>): its size is bounded by that work area, then it slides
    /// back the least possible. Nothing happens when that monitor cannot be read.
    /// </summary>
    private void FitWhereItIs(Window window)
    {
        if (DisplayMonitors.OfWindow(_hwnd) is not { } corner)
        {
            return;
        }

        // Bounded anew, the window may change size (from its top-left corner): no re-anchoring meanwhile.
        _reanchoring = true;
        try
        {
            var area = corner.WorkArea;
            BoundTo(window, area);
            window.UpdateLayout();

            var (x, y) = corner.ToDips();
            var rect = WindowPlacement.FitInside(new RectD(x, y, window.ActualWidth, window.ActualHeight), area.ToDips());
            _anchor = Anchor.User;
            _placedOn = area;
            MoveTo(rect, area);
        }
        finally
        {
            _reanchoring = false;
        }
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
        // During a drag, the window is the user's to place: a rescale for another DPI resizes it, and the monitor it
        // belonged to is no longer the right one. Its drop places it.
        if (!IsShown || _moving || _reanchoring || _placedOn is not { } area || _placedRect is not { } current)
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
    /// sees fit. Once that is done, it is placed again on the same monitor as it was shown; a window the user moved is
    /// only kept inside the work area where it is (a change of the display scale).
    /// During a drag onto a monitor with another DPI, nothing is done: the drop places the window.
    /// </summary>
    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        if (!IsShown || _moving || _window is not { } window)
        {
            return;
        }

        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!IsShown || _moving)
            {
                return;
            }

            window.UpdateLayout();
            if (_anchor == Anchor.User)
            {
                FitWhereItIs(window);
            }
            else if (_placedOn is { } area)
            {
                Place(window, area);
            }
        });
    }

    /// <summary>
    /// Windows' move loop, which a drag of the title bar runs (<see cref="Window.DragMove"/>): the window is left alone
    /// from its start (<see cref="_moving"/>) until the drop is handled, once the loop has ended and the layout has
    /// caught up with a DPI change on the way (<see cref="OnDropped"/>).
    /// </summary>
    private nint OnWindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmEnterSizeMove:
                _moving = true;
                break;
            case WmExitSizeMove when _moving && _window is { } window:
                window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, OnDropped);
                break;
        }

        return 0;
    }

    /// <summary>The window was dropped: it stays there, brought fully inside the work area of its monitor.</summary>
    private void OnDropped()
    {
        if (!_moving)
        {
            return;
        }

        _moving = false;
        if (IsShown)
        {
            FitWhereItIs(_window!);
        }
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

    /// <summary>What the window's place is relative to, re-applied when its DPI changes.</summary>
    private enum Anchor
    {
        /// <summary>Centered on the work area of its monitor.</summary>
        Screen,

        /// <summary>Where the user dropped it.</summary>
        User,
    }
}
