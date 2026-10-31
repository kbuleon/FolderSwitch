namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// Keeps FolderSwitch to one instance per session. The first instance owns the named mutex
/// <c>Local\FolderSwitch.SingleInstance</c> and waits on the auto-reset event
/// <c>Local\FolderSwitch.Activate</c>; a later one signals that event (so the first shows its window)
/// and gets <c>null</c>, its cue to exit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread.</b> A dedicated background thread acquires the mutex, waits for the event and releases
/// the mutex on <see cref="Dispose"/>: a mutex belongs to the thread that acquired it, and another
/// thread of the same process asking again would get it again (it is reentrant), so a second
/// <see cref="TryAcquire"/> in the same process fails as it would in another process. A mutex left
/// abandoned by an instance that crashed is taken over.
/// </para>
/// <para>
/// <b>Start-up race.</b> The event is created (or opened) before the mutex is asked for, by every
/// instance: when a later instance finds the mutex taken, the event already exists and is held by the
/// first one, so its signal is kept until the first one waits for it.
/// </para>
/// <para>
/// <b>Notifications.</b> <see cref="ActivationRequested"/> is raised once per signal, posted to the
/// <see cref="SynchronizationContext"/> given to <see cref="TryAcquire"/>, or raised on the waiting
/// thread without one (a handler must then return quickly). Not raised after <see cref="Dispose"/>,
/// including a notification already posted. Signals sent in a burst may be merged into one.
/// </para>
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private const string DefaultName = "FolderSwitch";

    private readonly string _name;
    private readonly SynchronizationContext? _context;
    private readonly EventWaitHandle? _activate;
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _thread;
    private volatile bool _listening; // set by the thread before it reports the mutex acquired
    private int _disposed;

    private SingleInstance(string name, EventWaitHandle? activate, SynchronizationContext? context)
    {
        _name = name;
        _activate = activate;
        _context = context;
        _thread = new Thread(Run) { IsBackground = true, Name = "FolderSwitch single instance" };
    }

    /// <summary>Raised when another instance asked this one to show itself.</summary>
    public event EventHandler? ActivationRequested;

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Becomes the single instance, or, if there is one already, signals it and returns <c>null</c>.
    /// </summary>
    /// <param name="name">The prefix of the named objects (<c>FolderSwitch</c>; another for tests).</param>
    /// <param name="context">Where <see cref="ActivationRequested"/> is posted (the UI thread's context).</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or holds a backslash.</exception>
    public static SingleInstance? TryAcquire(string name = DefaultName, SynchronizationContext? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("The name cannot hold a backslash.", nameof(name));
        }

        var activate = CreateActivationEvent(ActivationEventName(name));
        var instance = new SingleInstance(name, activate, context);
        bool acquired;
        try
        {
            acquired = instance.Start();
        }
        catch
        {
            instance.Dispose();
            throw;
        }

        if (acquired)
        {
            return instance;
        }

        activate?.Set();
        instance.Dispose();
        return null;
    }

    /// <summary>The name of the mutex for objects named after <paramref name="name"/>.</summary>
    internal static string MutexName(string name) => $@"Local\{name}.SingleInstance";

    /// <summary>The name of the activation event for objects named after <paramref name="name"/>.</summary>
    internal static string ActivationEventName(string name) => $@"Local\{name}.Activate";

    /// <summary>Releases the mutex, so that another instance may start, and stops the waiting thread.</summary>
    /// <remarks>
    /// Waits for that thread to end, except from the thread itself (a handler raised without a
    /// context): the mutex is then released once the handler returns.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_listening)
        {
            // The thread releases the mutex and disposes the handles on its way out.
            _stop.Set();
            if (Environment.CurrentManagedThreadId != _thread.ManagedThreadId)
            {
                _thread.Join();
            }

            return;
        }

        // The thread never listened: it has ended, or ends, without touching the handles.
        _stop.Dispose();
        _activate?.Dispose();
    }

    /// <summary>
    /// The activation event, created if needed; <c>null</c> if it cannot be opened (created with
    /// other access rights, by an elevated instance for one).
    /// </summary>
    private static EventWaitHandle? CreateActivationEvent(string name)
    {
        try
        {
            return new EventWaitHandle(false, EventResetMode.AutoReset, name);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return null;
        }
    }

    /// <summary>Starts the waiting thread; returns whether it acquired the mutex (it ends at once if not).</summary>
    private bool Start()
    {
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread.Start(acquired);
        return acquired.Task.GetAwaiter().GetResult();
    }

    private void Run(object? state)
    {
        var acquired = (TaskCompletionSource<bool>)state!;
        Mutex? mutex = null;
        var owned = false;
        try
        {
            try
            {
                mutex = new Mutex(false, MutexName(_name));
                owned = Acquire(mutex);
            }
            catch (UnauthorizedAccessException)
            {
                // Created with other access rights: an elevated instance runs.
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Some other kind of object holds the name: as good as taken.
            }
        }
        catch (Exception e)
        {
            mutex?.Dispose();
            acquired.SetException(e);
            return;
        }

        if (!owned)
        {
            mutex?.Dispose();
            acquired.SetResult(false);
            return;
        }

        _listening = true;
        acquired.SetResult(true);
        try
        {
            Listen();
        }
        finally
        {
            try
            {
                mutex!.ReleaseMutex();
            }
            finally
            {
                mutex!.Dispose();
                _stop.Dispose();
                _activate?.Dispose();
            }
        }
    }

    private static bool Acquire(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Its previous owner ended without releasing it: this thread owns it now.
            return true;
        }
    }

    /// <summary>On the waiting thread: raises the event for each signal until <see cref="Dispose"/>.</summary>
    private void Listen()
    {
        WaitHandle[] handles = _activate is null ? [_stop] : [_stop, _activate];

        // Ends on the stop event only, which Dispose sets: the handles stay valid until then.
        while (WaitHandle.WaitAny(handles) != 0)
        {
            Raise();
        }
    }

    private void Raise()
    {
        if (_context is null)
        {
            RaiseNow();
        }
        else
        {
            _context.Post(static state => ((SingleInstance)state!).RaiseNow(), this);
        }
    }

    private void RaiseNow()
    {
        if (!IsDisposed)
        {
            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
