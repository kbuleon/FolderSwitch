// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A scripted <see cref="IBrowserAddressReader"/>: <see cref="Responder"/> answers each attempt by its number (1 for the
/// first), and the calls are recorded. Safe to call from the thread pool.
/// </summary>
internal sealed class FakeBrowserAddressReader : IBrowserAddressReader
{
    private int _calls;

    /// <summary>The answer to attempt number n; by default <c>null</c> every time.</summary>
    public Func<int, BrowserPage?> Responder { get; set; } = _ => null;

    /// <summary>While set, an attempt waits for it to be signalled before it answers (a read still under way).</summary>
    public ManualResetEventSlim? Gate { get; set; }

    /// <summary>Signalled when an attempt starts, before it waits for <see cref="Gate"/>.</summary>
    public ManualResetEventSlim Started { get; } = new();

    public int Calls => Volatile.Read(ref _calls);

    public nint LastWindow { get; private set; }

    public BrowserKind LastBrowser { get; private set; }

    public BrowserPage? TryRead(nint window, BrowserKind browser)
    {
        var number = Interlocked.Increment(ref _calls);
        LastWindow = window;
        LastBrowser = browser;
        Started.Set();
        Gate?.Wait(TimeSpan.FromSeconds(10));
        return Responder(number);
    }
}
