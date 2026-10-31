using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// A background STA thread that runs work items one at a time, for COM calls that need a
/// single-threaded apartment. A caller waits for its item with a timeout. An item that overruns keeps
/// running: the owner is expected to give up on the worker (<see cref="Dispose"/>) and to watch
/// <see cref="IsAlive"/>, since nothing bounds how long that item takes (a COM call into a hung
/// process waits for it). The thread ends once that item returns; items still queued behind it are
/// dropped without running.
/// </summary>
internal sealed class StaWorker : IDisposable
{
    /// <summary>
    /// A wait that times out may return up to one tick of the system timer late (15.6 ms by default):
    /// the last stretch before the deadline is waited by yielding instead.
    /// </summary>
    private static readonly TimeSpan TimerTick = TimeSpan.FromMilliseconds(16);

    private readonly BlockingCollection<WorkItem> _queue = new();
    private readonly Thread _thread;
    private volatile bool _disposed;

    public StaWorker(string name)
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Whether the thread still runs: idle or busy before <see cref="Dispose"/>, busy with its last item after.</summary>
    public bool IsAlive => _thread.IsAlive;

    /// <summary>
    /// Runs <paramref name="work"/> on the worker thread and waits for it at most
    /// <paramref name="timeout"/>, returning as soon as it is done. Returns <c>false</c> if it did not
    /// finish in time: its result is dropped, and if it has not started yet it never will. An
    /// exception thrown by <paramref name="work"/> is rethrown here.
    /// </summary>
    public bool TryRun<T>(Func<T> work, TimeSpan timeout, out T? result)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        var item = new WorkItem<T>(work, owners: 2);
        Enqueue(item);
        try
        {
            var done = timeout > TimerTick && item.Done.Wait(timeout - TimerTick);
            while (!done && Stopwatch.GetTimestamp() < deadline)
            {
                Thread.Yield();
                done = item.Done.IsSet;
            }

            if (!done && !item.Done.IsSet)
            {
                item.Abandon();
                result = default;
                return false;
            }

            item.Error?.Throw();
            result = item.Result;
            return true;
        }
        finally
        {
            item.Release();
        }
    }

    /// <summary>Queues <paramref name="work"/> without waiting for it; its exceptions are ignored.</summary>
    public void Post(Action work) => Enqueue(new WorkItem<bool>(
        () =>
        {
            work();
            return true;
        },
        owners: 1));

    /// <summary>Stops taking work; the thread ends after the item it is running, if any.</summary>
    public void Dispose()
    {
        _disposed = true;
        _queue.CompleteAdding();
    }

    private void Enqueue(WorkItem item)
    {
        try
        {
            _queue.Add(item);
        }
        catch (InvalidOperationException)
        {
            // Never queued: nobody else holds it.
            item.Done.Dispose();
            throw new ObjectDisposedException(nameof(StaWorker));
        }
    }

    private void Run()
    {
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            // After Dispose, only the item already running may go on: the queued ones are dropped.
            item.Execute(skip: _disposed);
        }

        _queue.Dispose();
    }

    private abstract class WorkItem(int owners)
    {
        private int _owners = owners;
        private int _abandoned;

        public ManualResetEventSlim Done { get; } = new();

        public ExceptionDispatchInfo? Error { get; protected set; }

        protected bool IsAbandoned => Volatile.Read(ref _abandoned) != 0;

        public void Abandon() => Interlocked.Exchange(ref _abandoned, 1);

        /// <summary>Runs the work unless it is to be skipped or was abandoned, then signals and lets go of it.</summary>
        public void Execute(bool skip)
        {
            try
            {
                if (!skip && !IsAbandoned)
                {
                    Run();
                }
            }
            finally
            {
                Done.Set();
                Release();
            }
        }

        /// <summary>The waiter and the worker each let go once; the last one disposes the event.</summary>
        public void Release()
        {
            if (Interlocked.Decrement(ref _owners) == 0)
            {
                Done.Dispose();
            }
        }

        protected abstract void Run();
    }

    private sealed class WorkItem<T>(Func<T> work, int owners) : WorkItem(owners)
    {
        public T? Result { get; private set; }

        protected override void Run()
        {
            try
            {
                Result = work();
            }
            catch (Exception e)
            {
                Error = ExceptionDispatchInfo.Capture(e);
            }
        }
    }
}
