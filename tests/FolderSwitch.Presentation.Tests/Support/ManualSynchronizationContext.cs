using System.Collections.Concurrent;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A stand-in for the UI thread's context: what is posted waits in a queue until <see cref="Pump"/> runs it on the
/// calling thread. Installs itself as the current context for the test thread, and restores the previous one when disposed.
/// </summary>
internal sealed class ManualSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly SynchronizationContext? _previous = Current;

    public ManualSynchronizationContext() => SetSynchronizationContext(this);

    public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

    /// <summary>Runs everything posted so far; true when something ran.</summary>
    public bool Pump()
    {
        var ran = false;
        while (_queue.TryDequeue(out var work))
        {
            work.Callback(work.State);
            ran = true;
        }

        return ran;
    }

    /// <summary>Pumps until <paramref name="condition"/> holds; fails the test after five real seconds.</summary>
    public void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Pump();
            if (condition())
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, "The condition was not reached in time.");
            Thread.Sleep(1);
        }
    }

    public void Dispose() => SetSynchronizationContext(_previous);
}
