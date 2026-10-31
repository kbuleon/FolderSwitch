using System.ComponentModel;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.Hotkeys;

/// <summary>
/// The global hotkey. A dedicated background thread runs a Win32 message loop with a message-only
/// window (<c>RegisterHotKey</c> and session notifications) and, while a modifier-only hotkey is
/// registered or a capture runs, a <c>WH_KEYBOARD_LL</c> hook feeding a <see cref="ModifierChordDetector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Public methods may be called from any thread: they run their work on the hook thread and wait for
/// it. <see cref="Triggered"/> and capture callbacks are posted to the <see cref="SynchronizationContext"/>
/// given to the constructor; without one they run on the hook thread, where they must return quickly
/// since keyboard input waits for that thread. A notification already posted is dropped if the hotkey
/// was unregistered, replaced or disposed (or the capture ended) in the meantime.
/// </para>
/// <para>
/// The hook callback only updates state, injects the mask key when the Start menu must stay closed
/// and posts messages; it never runs user code and never blocks. Injected events are ignored.
/// </para>
/// <para>
/// <b>Keeping the hook alive.</b> Windows silently removes a low-level hook whose callback does not return
/// within its timeout (a saturated CPU, a working set paged out after a long idle), and nothing tells
/// the process. The hook thread runs at the highest priority short of time-critical, so that its
/// callback is scheduled before ordinary work, and the hook is replaced by a fresh one (a cheap
/// safety net): when the session is unlocked or reconnected, when the system resumes from sleep, and
/// every <see cref="RearmInterval"/> while no capture runs and no modifier key is down, so never in
/// the middle of a chord (a re-arm put off that way is tried again after <see cref="RearmRetry"/>).
/// </para>
/// </remarks>
public sealed class HotkeyService : IHotkeyService
{
    /// <summary>How often the keyboard hook is replaced by a fresh one, in case Windows removed it.</summary>
    internal static readonly TimeSpan RearmInterval = TimeSpan.FromMinutes(5);

    /// <summary>How soon a periodic re-arm put off by a capture or a modifier key held down is tried again.</summary>
    internal static readonly TimeSpan RearmRetry = TimeSpan.FromSeconds(5);

    private const uint WmInvoke = PInvoke.WM_APP + 1;
    private const uint WmRaise = PInvoke.WM_APP + 2;
    private const uint WmUpdateHook = PInvoke.WM_APP + 3;

    private const int HotkeyId = 1;
    private const int ProbeId = 2;

    private const nuint RearmTimerId = 1;

    /// <summary>The modifier keys as a low-level hook reports them: the left and right ones.</summary>
    private static readonly int[] ModifierKeys =
    [
        VirtualKeys.LControl, VirtualKeys.RControl, VirtualKeys.LMenu, VirtualKeys.RMenu,
        VirtualKeys.LShift, VirtualKeys.RShift, VirtualKeys.LWin, VirtualKeys.RWin,
    ];

    private readonly SynchronizationContext? _context;
    private readonly IKeyboardSystem _keyboard;
    private readonly bool _processRealInput;
    private readonly Thread _thread;

    // ---- Shared between threads ----
    private readonly Lock _gate = new();
    private readonly Queue<WorkItem> _work = new(); // guarded by _gate
    private bool _accepting; // guarded by _gate; false before startup and from shutdown on
    private HWND _window; // written before _accepting becomes true and after it becomes false
    private int _disposed;
    private int _generation; // changes whenever posted notifications must be dropped

    // ---- Hook thread only ----
    private readonly LowLevelKeyboardHook _hook;
    private readonly Queue<Action> _outbox = new();
    private readonly HashSet<int> _held = []; // physical keys seen down and let through
    private readonly HashSet<int> _swallowed = []; // keys whose press was swallowed: their release is too
    private readonly HashSet<int> _maskOnRelease = []; // Win/Alt left alone by a swallowed key
    private ModifierChordDetector? _detector;
    private RegisteredHotkey? _registered;
    private Hotkey? _current;
    private CaptureSession? _capture;
    private KeyModifiers? _captureCandidate; // modifiers held when the first one of the sequence was released
    private bool _captureSawRegularKey;
    private bool _updateHookPosted;
    private bool _sessionNotifications;
    private HPOWERNOTIFY _resumeNotifications;
    private ExceptionDispatchInfo? _callbackError;
    private WNDPROC? _windowProc; // referenced for as long as the window class exists
    private HINSTANCE _instance;
    private string? _className;

    /// <param name="context">Where <see cref="Triggered"/> and capture callbacks are posted (the UI thread's context).</param>
    public HotkeyService(SynchronizationContext? context)
        : this(context, Win32KeyboardSystem.Instance, processRealInput: true)
    {
    }

    /// <param name="processRealInput">
    /// <c>false</c> for tests: the hook is really installed but passes every physical event on
    /// untouched; key events come only through <see cref="SimulateKey"/>.
    /// </param>
    internal HotkeyService(SynchronizationContext? context, IKeyboardSystem keyboard, bool processRealInput)
    {
        _context = context;
        _keyboard = keyboard;
        _processRealInput = processRealInput;
        _hook = new LowLevelKeyboardHook(OnHookKey, OnCallbackError);

        // Keyboard input waits for the hook callback: it must be scheduled ahead of ordinary work (not time-critical,
        // which would compete with the system itself).
        _thread = new Thread(Run) { IsBackground = true, Name = "FolderSwitch hotkeys", Priority = ThreadPriority.Highest };

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread.Start(started);
        started.Task.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    /// <remarks>Raised on the constructor's context, or on the hook thread without one.</remarks>
    public event EventHandler? Triggered;

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private bool HookNeeded =>
        _detector is not null || _capture is not null || _swallowed.Count > 0 || _maskOnRelease.Count > 0;

    /// <inheritdoc/>
    /// <remarks>
    /// When the registration fails (blocking issue or combination taken), the previous hotkey stays in
    /// effect. A modifier-only hotkey installs the keyboard hook; any other uses <c>RegisterHotKey</c>.
    /// </remarks>
    public IReadOnlyList<HotkeyIssue> Register(Hotkey h)
    {
        ArgumentNullException.ThrowIfNull(h);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var issues = HotkeyRules.Validate(h);
        if (issues.Any(HotkeyRules.IsBlocking))
        {
            return issues;
        }

        var registered = false;
        Invoke(() => registered = RegisterCore(h));
        return registered ? issues : [.. issues, HotkeyIssue.TakenByOtherApp];
    }

    /// <inheritdoc/>
    public void Unregister()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Invoke(() =>
        {
            ReleaseRegistration();
            UpdateHook();
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The keyboard hook is installed for the capture whatever the registered hotkey. The captured
    /// regular key is swallowed (press, auto-repeat and release), so the combination does nothing else;
    /// a key without modifier (Escape, say) goes through. Win and Alt releases are masked so the Start
    /// menu and menu bars stay closed. A new capture replaces the one in progress, which then delivers
    /// nothing.
    /// </remarks>
    public IDisposable BeginCapture(Action<Hotkey> onCaptured)
    {
        ArgumentNullException.ThrowIfNull(onCaptured);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var session = new CaptureSession(onCaptured);
        Invoke(() =>
        {
            _capture?.Cancel();
            _capture = session;
            _detector?.Reset();
            ResetCaptureSequence();
            try
            {
                UpdateHook();
            }
            catch
            {
                _capture = null;
                throw;
            }
        });
        return new CaptureHandle(this, session);
    }

    /// <inheritdoc/>
    /// <remarks>The combination currently registered by this service counts as free.</remarks>
    public bool Probe(Hotkey h)
    {
        ArgumentNullException.ThrowIfNull(h);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (h.IsModifierOnly)
        {
            return true;
        }

        var free = false;
        Invoke(() => free = (_registered is not null && h == _current) || RegisteredHotkey.IsFree(_window, ProbeId, h));
        return free;
    }

    /// <summary>Removes the hook and the registration, then stops and joins the hook thread.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Increment(ref _generation);
        try
        {
            Invoke(() =>
            {
                ReleaseAll();
                PInvoke.PostQuitMessage(0);
            });
        }
        catch (ObjectDisposedException)
        {
            // The hook thread is already gone.
        }

        if (Environment.CurrentManagedThreadId != _thread.ManagedThreadId)
        {
            _thread.Join();
        }
    }

    // ---- Test seams ----

    /// <summary>Test seam: whether the low-level keyboard hook is installed.</summary>
    internal bool IsHookInstalled
    {
        get
        {
            var installed = false;
            Invoke(() => installed = _hook.IsInstalled);
            return installed;
        }
    }

    /// <summary>
    /// Test seam: handles a physical key event on the hook thread exactly as the hook callback does
    /// (ignored while the hook is not installed). Returns whether the event would be swallowed.
    /// </summary>
    internal bool SimulateKey(int virtualKey, bool isKeyDown)
    {
        var swallowed = false;
        Invoke(() => swallowed = _hook.IsInstalled && HandleKey(virtualKey, isKeyDown));
        return swallowed;
    }

    /// <summary>Test seam: a <c>WM_WTSSESSION_CHANGE</c> notification with this reason.</summary>
    internal void SimulateSessionChange(uint reason) => Invoke(() => OnSessionChange(reason));

    /// <summary>Test seam: a <c>WM_POWERBROADCAST</c> notification of the system resuming from sleep.</summary>
    internal void SimulateResume() => Invoke(() => OnPowerBroadcast(PInvoke.PBT_APMRESUMEAUTOMATIC));

    /// <summary>Test seam: the periodic re-arm timer fires now.</summary>
    internal void SimulateRearmTick() => Invoke(OnRearmTimer);

    /// <summary>Test seam: how many times a keyboard hook was installed (a re-arm installs a fresh one).</summary>
    internal int HookInstallCount
    {
        get
        {
            var count = 0;
            Invoke(() => count = _hook.InstallCount);
            return count;
        }
    }

    /// <summary>
    /// Test seam: returns once the hook thread has handled every message posted before the call, so
    /// that every notification due so far has been posted to the context.
    /// </summary>
    internal void Flush() => Invoke(static () => { });

    // ---- Hook thread: lifetime ----

    private void Run(object? state)
    {
        var started = (TaskCompletionSource)state!;
        try
        {
            CreateWindow();
            RegisterSessionNotifications();
            RegisterResumeNotifications();
            ScheduleRearm(RearmInterval);
            lock (_gate)
            {
                _accepting = true;
            }
        }
        catch (Exception e)
        {
            DestroyWindow();
            started.SetException(e);
            return;
        }

        started.SetResult();
        try
        {
            RunMessageLoop();
        }
        finally
        {
            Shutdown();
        }
    }

    private void RunMessageLoop()
    {
        while (true)
        {
            var result = PInvoke.GetMessage(out var message, HWND.Null, 0, 0);
            if (result.Value == 0)
            {
                return; // WM_QUIT
            }

            if (result.Value == -1)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            PInvoke.DispatchMessage(in message);

            // An exception thrown in a callback could not unwind through the native caller: it is
            // rethrown here, as an unhandled exception of this thread.
            if (_callbackError is { } error)
            {
                _callbackError = null;
                error.Throw();
            }
        }
    }

    private void Shutdown()
    {
        WorkItem[] pending;
        lock (_gate)
        {
            _accepting = false;
            pending = [.. _work];
            _work.Clear();
        }

        foreach (var item in pending)
        {
            item.Fail(new ObjectDisposedException(nameof(HotkeyService)));
        }

        ReleaseAll();
        if (_sessionNotifications)
        {
            PInvoke.WTSUnRegisterSessionNotification(_window);
            _sessionNotifications = false;
        }

        if (_resumeNotifications.Value != 0 && OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            PInvoke.UnregisterSuspendResumeNotification(_resumeNotifications);
            _resumeNotifications = default;
        }

        PInvoke.KillTimer(_window, RearmTimerId);
        DestroyWindow();
    }

    private unsafe void CreateWindow()
    {
        _instance = PInvoke.GetModuleHandle(default(PCWSTR));
        WNDPROC windowProc = WindowProc;
        _windowProc = windowProc;

        // One class per instance: several services may live in the same process (tests do).
        var className = "FolderSwitch.Hotkeys." + Guid.NewGuid().ToString("N");
        fixed (char* name = className)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = windowProc,
                hInstance = _instance,
                lpszClassName = name,
            };
            if (PInvoke.RegisterClassEx(in windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _className = className;
            _window = PInvoke.CreateWindowEx(0, name, default, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, default, _instance, null);
            if (_window.IsNull)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
    }

    private unsafe void DestroyWindow()
    {
        if (!_window.IsNull)
        {
            PInvoke.DestroyWindow(_window);
            _window = default;
        }

        if (_className is not null)
        {
            fixed (char* name = _className)
            {
                PInvoke.UnregisterClass(name, _instance);
            }

            _className = null;
        }

        _windowProc = null;
    }

    private void RegisterSessionNotifications()
    {
        // May fail very early in a logon if the Remote Desktop services are not ready yet: retried on
        // each registration.
        if (!_sessionNotifications)
        {
            _sessionNotifications = PInvoke.WTSRegisterSessionNotification(_window, PInvoke.NOTIFY_FOR_THIS_SESSION);
        }
    }

    /// <summary>
    /// <c>WM_POWERBROADCAST</c> on resume from sleep: a message-only window receives no broadcast, so it asks for these
    /// notifications by handle. Best effort: without them, the periodic re-arm still restores a lost hook.
    /// </summary>
    private unsafe void RegisterResumeNotifications()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            return;
        }

        try
        {
            _resumeNotifications = PInvoke.RegisterSuspendResumeNotification(
                new HANDLE(_window.Value), REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
        }
        catch (EntryPointNotFoundException)
        {
            // Not available: the periodic re-arm remains.
        }
    }

    /// <summary>Arms (or re-arms) the periodic re-arm timer of the hook.</summary>
    private void ScheduleRearm(TimeSpan delay) =>
        PInvoke.SetTimer(_window, RearmTimerId, (uint)delay.TotalMilliseconds, null);

    // ---- Marshaling between threads ----

    /// <summary>Runs <paramref name="action"/> on the hook thread and waits for it, rethrowing its exception.</summary>
    private void Invoke(Action action)
    {
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            // Already on the hook thread: a notification raised without a context.
            action();
            return;
        }

        var item = new WorkItem(action);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(!_accepting, this);
            _work.Enqueue(item);
            if (!PInvoke.PostMessage(_window, WmInvoke, default, default))
            {
                // Still queued but abandoned: it must not run after the caller saw the failure.
                item.Abandon();
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        item.Wait();
    }

    private void RunPendingWork()
    {
        while (true)
        {
            WorkItem? item;
            lock (_gate)
            {
                if (!_work.TryDequeue(out item))
                {
                    return;
                }
            }

            item.Run();
        }
    }

    /// <summary>From the hook callback: queues a notification, delivered from the message loop.</summary>
    private void Raise(Action notification)
    {
        _outbox.Enqueue(notification);
        PInvoke.PostMessage(_window, WmRaise, default, default);
    }

    private void DeliverOutbox()
    {
        while (_outbox.TryDequeue(out var notification))
        {
            Deliver(notification);
        }
    }

    private void Deliver(Action notification)
    {
        if (_context is null)
        {
            notification();
        }
        else
        {
            _context.Post(static state => ((Action)state!)(), notification);
        }
    }

    /// <summary>A <see cref="Triggered"/> notification, dropped if the registration changed before it runs.</summary>
    private Action TriggerNotification()
    {
        var generation = Volatile.Read(ref _generation);
        return () =>
        {
            if (Volatile.Read(ref _generation) == generation)
            {
                Triggered?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    private void OnCallbackError(Exception e)
    {
        _callbackError ??= ExceptionDispatchInfo.Capture(e);
        PInvoke.PostMessage(_window, WmRaise, default, default); // wakes the message loop up
    }

    // ---- Hook thread: messages ----

    private LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            switch (message)
            {
                case WmInvoke:
                    RunPendingWork();
                    return default;
                case WmRaise:
                    DeliverOutbox();
                    return default;
                case WmUpdateHook:
                    _updateHookPosted = false;
                    UpdateHook();
                    return default;
                case PInvoke.WM_HOTKEY:
                    OnRegisteredHotkey((int)wParam.Value);
                    return default;
                case PInvoke.WM_WTSSESSION_CHANGE:
                    OnSessionChange((uint)wParam.Value);
                    return default;
                case PInvoke.WM_TIMER when wParam.Value == RearmTimerId:
                    OnRearmTimer();
                    return default;
                case PInvoke.WM_POWERBROADCAST:
                    OnPowerBroadcast((uint)wParam.Value);
                    return (LRESULT)1;
            }
        }
        catch (Exception e)
        {
            OnCallbackError(e);
            return default;
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void OnRegisteredHotkey(int id)
    {
        // During a capture the combination is being captured, not used.
        if (id == HotkeyId && _registered is not null && _capture is null)
        {
            Deliver(TriggerNotification());
        }
    }

    private void OnSessionChange(uint reason)
    {
        // Key releases are not seen while the session is locked or switched away: start afresh.
        ResetKeyState();
        if (reason is PInvoke.WTS_SESSION_UNLOCK or PInvoke.WTS_CONSOLE_CONNECT or PInvoke.WTS_REMOTE_CONNECT)
        {
            // Windows silently removes a low-level hook whose callback timed out: cheap safety net.
            _hook.Reinstall();
        }

        UpdateHook();
    }

    private void OnPowerBroadcast(uint kind)
    {
        if (kind is not (PInvoke.PBT_APMRESUMEAUTOMATIC or PInvoke.PBT_APMRESUMESUSPEND))
        {
            return;
        }

        // Back from sleep: key releases may have been missed, and a callback that ran late as the system went down or
        // came back may have had the hook removed.
        ResetKeyState();
        _hook.Reinstall();
        UpdateHook();
    }

    /// <summary>
    /// The periodic re-arm: the hook is replaced by a fresh one when the keyboard is quiet (no capture, no modifier
    /// down, so no chord under way), otherwise tried again soon. Replacing it loses no key event: the new hook is in
    /// place before the old one goes, and neither is called before this returns to the message loop.
    /// </summary>
    private void OnRearmTimer()
    {
        if (!_hook.IsInstalled)
        {
            ScheduleRearm(RearmInterval);
            return;
        }

        // Releases missed while the hook was gone are forgotten first, as the next key event would.
        ForgetReleasedKeys();
        if (_capture is not null || HeldModifiers() != KeyModifiers.None || IsAnyModifierDown())
        {
            ScheduleRearm(RearmRetry);
            return;
        }

        _hook.Reinstall();
        ScheduleRearm(RearmInterval);
    }

    private bool IsAnyModifierDown()
    {
        foreach (var key in ModifierKeys)
        {
            if (_keyboard.IsKeyDown(key))
            {
                return true;
            }
        }

        return false;
    }

    // ---- Hook thread: registration ----

    private bool RegisterCore(Hotkey hotkey)
    {
        RegisterSessionNotifications();
        var previous = _current;
        ReleaseRegistration();

        var registered = TryApply(hotkey);
        if (!registered && previous is not null)
        {
            TryApply(previous);
        }

        try
        {
            UpdateHook();
        }
        catch
        {
            ReleaseRegistration();
            throw;
        }

        return registered;
    }

    private bool TryApply(Hotkey hotkey)
    {
        if (hotkey.IsModifierOnly)
        {
            _detector = new ModifierChordDetector(hotkey.Modifiers);
        }
        else
        {
            _registered = RegisteredHotkey.TryRegister(_window, HotkeyId, hotkey);
            if (_registered is null)
            {
                return false;
            }
        }

        _current = hotkey;
        return true;
    }

    private void ReleaseRegistration()
    {
        Interlocked.Increment(ref _generation);
        _registered?.Dispose();
        _registered = null;
        _detector = null;
        _current = null;
    }

    private void ReleaseAll()
    {
        _capture?.Cancel();
        _capture = null;
        ReleaseRegistration();
        _hook.Uninstall();
        ResetKeyState();
        _outbox.Clear();
    }

    /// <summary>Installs or removes the hook as needed. Not from inside the hook callback.</summary>
    private void UpdateHook()
    {
        var needed = HookNeeded;
        if (needed == _hook.IsInstalled)
        {
            return;
        }

        if (needed)
        {
            _hook.Install();
        }
        else
        {
            _hook.Uninstall();
        }

        // A fresh hook knows nothing of the keys already down.
        ResetKeyState();
    }

    private void PostUpdateHook()
    {
        if (!_updateHookPosted)
        {
            _updateHookPosted = true;
            PInvoke.PostMessage(_window, WmUpdateHook, default, default);
        }
    }

    private void ResetKeyState()
    {
        _held.Clear();
        _swallowed.Clear();
        _maskOnRelease.Clear();
        _detector?.Reset();
        ResetCaptureSequence();
    }

    private void ResetCaptureSequence()
    {
        _captureCandidate = null;
        _captureSawRegularKey = false;
    }

    // ---- Hook thread: key events ----

    private bool OnHookKey(int virtualKey, bool isKeyDown) => _processRealInput && HandleKey(virtualKey, isKeyDown);

    private bool HandleKey(int virtualKey, bool isKeyDown)
    {
        var swallow = ProcessKey(virtualKey, isKeyDown);
        if (!HookNeeded)
        {
            // Removed from the message loop, not from inside its own callback.
            PostUpdateHook();
        }

        return swallow;
    }

    /// <returns>Whether to swallow the event.</returns>
    private bool ProcessKey(int vk, bool isKeyDown)
    {
        if (_swallowed.Contains(vk))
        {
            if (!isKeyDown)
            {
                _swallowed.Remove(vk);
            }

            return true;
        }

        ForgetReleasedKeys();

        if (isKeyDown)
        {
            var isRepeat = !_held.Add(vk);
            if (_capture is not null)
            {
                return CaptureKeyDown(vk, isRepeat);
            }

            if (_detector is not null)
            {
                Apply(_detector.OnKeyDown(vk, isRepeat), alreadyMasked: false);
            }

            return false;
        }

        var wasHeld = _held.Remove(vk);
        var maskPending = _maskOnRelease.Remove(vk);
        if (_capture is not null)
        {
            CaptureKeyUp(vk, wasHeld);
            return false;
        }

        if (maskPending)
        {
            _keyboard.SendMaskKey();
        }

        if (_detector is not null)
        {
            Apply(_detector.OnKeyUp(vk), maskPending);
        }

        return false;
    }

    private void Apply(ChordStep step, bool alreadyMasked)
    {
        if (step.SuppressStartMenu && !alreadyMasked)
        {
            // Before the Win release goes through, which it does when the callback returns.
            _keyboard.SendMaskKey();
        }

        if (step.Triggered)
        {
            Raise(TriggerNotification());
        }
    }

    /// <summary>
    /// Key releases can be missed (secure desktop after Ctrl + Alt + Del or a UAC prompt, input sent
    /// to an elevated window, hook removed after a timeout); a stuck key would block the chord. Held
    /// keys the system reports as up are forgotten and detection restarts from scratch. During its own
    /// callback a key keeps its previous state: a key being released still reads as down, and a key
    /// pressed again after a missed release reads as up.
    /// </summary>
    private void ForgetReleasedKeys()
    {
        if (!HasReleasedKeys())
        {
            return;
        }

        _held.RemoveWhere(key => !_keyboard.IsKeyDown(key));
        _maskOnRelease.IntersectWith(_held);
        _detector?.Reset();
        ResetCaptureSequence();
    }

    private bool HasReleasedKeys()
    {
        foreach (var key in _held)
        {
            if (!_keyboard.IsKeyDown(key))
            {
                return true;
            }
        }

        return false;
    }

    private bool CaptureKeyDown(int vk, bool isRepeat)
    {
        if (isRepeat || VirtualKeys.ModifierOf(vk) is not null)
        {
            return false;
        }

        var modifiers = HeldModifiers();
        if (modifiers == KeyModifiers.None)
        {
            // Not a hotkey (Escape to cancel, say): it goes through, and this sequence can no longer
            // give a modifier-only hotkey.
            _captureSawRegularKey = true;
            return false;
        }

        // Modifiers + a regular key: that is the hotkey. The key is swallowed so the combination does
        // nothing else; Win or Alt then left alone must not open the Start menu or a menu bar.
        _held.Remove(vk);
        _swallowed.Add(vk);
        foreach (var key in _held)
        {
            if (VirtualKeys.ModifierOf(key) is KeyModifiers.Win or KeyModifiers.Alt)
            {
                _maskOnRelease.Add(key);
            }
        }

        CompleteCapture(new Hotkey(modifiers, vk));
        return true;
    }

    private void CaptureKeyUp(int vk, bool wasHeld)
    {
        var modifier = VirtualKeys.ModifierOf(vk);
        if (modifier is KeyModifiers.Win or KeyModifiers.Alt)
        {
            // No Start menu, no menu bar while capturing.
            _keyboard.SendMaskKey();
        }

        if (wasHeld && modifier is { } released && !_captureSawRegularKey)
        {
            _captureCandidate ??= HeldModifiers() | released;
        }

        if (_held.Count > 0)
        {
            return;
        }

        var candidate = _captureCandidate;
        ResetCaptureSequence();
        if (candidate is { } chord && BitOperations.PopCount((uint)chord) >= 2)
        {
            CompleteCapture(new Hotkey(chord, null));
        }
    }

    private void CompleteCapture(Hotkey captured)
    {
        var session = _capture!;
        _capture = null;
        _detector?.Reset();
        ResetCaptureSequence();
        Raise(() =>
        {
            if (!IsDisposed)
            {
                session.Deliver(captured);
            }
        });
    }

    /// <summary>From <see cref="CaptureHandle"/>: ends the capture if it is still the current one.</summary>
    private void EndCapture(CaptureSession session)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Invoke(() =>
            {
                if (_capture != session)
                {
                    return;
                }

                _capture = null;
                _detector?.Reset();
                ResetCaptureSequence();
                UpdateHook();
            });
        }
        catch (ObjectDisposedException)
        {
            // Disposed meanwhile: nothing left to end.
        }
    }

    private KeyModifiers HeldModifiers()
    {
        var modifiers = KeyModifiers.None;
        foreach (var key in _held)
        {
            if (VirtualKeys.ModifierOf(key) is { } modifier)
            {
                modifiers |= modifier;
            }
        }

        return modifiers;
    }

    /// <summary>A unit of work run on the hook thread for a waiting caller.</summary>
    private sealed class WorkItem(Action action)
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _abandoned; // set under the gate before the item can be dequeued

        public void Abandon() => _abandoned = true;

        public void Run()
        {
            if (_abandoned)
            {
                return;
            }

            try
            {
                action();
                _completion.SetResult();
            }
            catch (Exception e)
            {
                _completion.SetException(e);
            }
        }

        public void Fail(Exception e) => _completion.TrySetException(e);

        public void Wait() => _completion.Task.GetAwaiter().GetResult();
    }

    private sealed class CaptureSession(Action<Hotkey> onCaptured)
    {
        private int _cancelled;

        /// <returns><c>true</c> the first time.</returns>
        public bool Cancel() => Interlocked.Exchange(ref _cancelled, 1) == 0;

        public void Deliver(Hotkey captured)
        {
            if (Volatile.Read(ref _cancelled) == 0)
            {
                onCaptured(captured);
            }
        }
    }

    private sealed class CaptureHandle(HotkeyService owner, CaptureSession session) : IDisposable
    {
        public void Dispose()
        {
            if (session.Cancel())
            {
                owner.EndCapture(session);
            }
        }
    }
}
