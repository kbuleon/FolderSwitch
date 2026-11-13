// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;

namespace FolderSwitch.Core.Services;

/// <summary>
/// Reads the folder an Explorer window or a file dialog shows, after the call: the capture of the call has a short
/// budget, and a folder it could not read in time is read again here, with more time.
/// </summary>
public interface ICurrentFolderReader
{
    /// <summary>
    /// One attempt to read the folder shown by <paramref name="window"/>, a window of <paramref name="kind"/>
    /// (<see cref="CallContextKind.Explorer"/> or <see cref="CallContextKind.FileDialog"/>) captured earlier. The attempt
    /// is bounded at 500 ms; calls are serialized, so a concurrent call queues behind the current one and may wait
    /// longer in all. Callable from any thread; never throws. Retries are the caller's.
    /// </summary>
    /// <returns>The folder, or <c>null</c> when it is unknown, when the window is gone, or on any failure.</returns>
    FolderLocation? TryRead(CallContextKind kind, nint window);
}
