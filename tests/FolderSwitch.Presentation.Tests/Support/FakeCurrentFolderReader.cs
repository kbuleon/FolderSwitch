// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A scripted <see cref="ICurrentFolderReader"/>: <see cref="Responder"/> answers each attempt by its number (1 for the
/// first), and the calls are recorded. Safe to call from the thread pool.
/// </summary>
internal sealed class FakeCurrentFolderReader : ICurrentFolderReader
{
    private int _calls;

    /// <summary>The answer to attempt number n; by default <c>null</c> every time.</summary>
    public Func<int, FolderLocation?> Responder { get; set; } = _ => null;

    public int Calls => Volatile.Read(ref _calls);

    public CallContextKind LastKind { get; private set; }

    public nint LastWindow { get; private set; }

    public FolderLocation? TryRead(CallContextKind kind, nint window)
    {
        var number = Interlocked.Increment(ref _calls);
        LastKind = kind;
        LastWindow = window;
        return Responder(number);
    }
}
