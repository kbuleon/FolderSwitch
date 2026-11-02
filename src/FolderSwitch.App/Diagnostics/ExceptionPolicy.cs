// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.Core.Diagnostics;

namespace FolderSwitch.App.Diagnostics;

/// <summary>
/// Which exceptions a resident application must not survive: those after which the process can no longer be trusted
/// to work, so that keeping it alive would leave a zombie holding the single-instance mutex and the keyboard hook.
/// </summary>
internal static class ExceptionPolicy
{
    /// <summary>The facility of the WPF composition and render-thread errors (<c>0x8898xxxx</c>, e.g. UCEERR_RENDERTHREADFAILURE).</summary>
    private const int MilFacilityMask = unchecked((int)0xFFFF0000);
    private const int MilFacility = unchecked((int)0x88980000);

    /// <summary>
    /// True for an exception (or one it wraps) that leaves the process unusable: out of memory, stack exhausted, a
    /// native fault (the types of <see cref="CriticalExceptions"/>, which the activation service of Core lets through
    /// as well), or a failure of WPF's render thread (after a GPU reset, say), after which nothing is drawn again.
    /// </summary>
    public static bool IsCritical(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (CriticalExceptions.IsCriticalType(e))
            {
                return true;
            }

            // The render thread failed: reported as a COM error of the composition facility (DUCE channel), or as
            // the InvalidOperationException that MediaContext raises when its render partition becomes a zombie.
            if (e is ExternalException && (e.HResult & MilFacilityMask) == MilFacility)
            {
                return true;
            }

            if (e is InvalidOperationException && e.TargetSite is { Name: "NotifyPartitionIsZombie" })
            {
                return true;
            }
        }

        return false;
    }
}
