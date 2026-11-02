// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.ExceptionServices;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>Runs code on a fresh STA thread and waits for it (xUnit runs tests on MTA threads).</summary>
internal static class Sta
{
    public static T Run<T>(Func<T> work)
    {
        T result = default!;
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }
        })
        { IsBackground = true, Name = "Test STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
        return result;
    }

    public static void Run(Action work) => Run<object?>(() =>
    {
        work();
        return null;
    });
}
