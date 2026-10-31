using System.Runtime.InteropServices;

namespace FolderSwitch.Core.Diagnostics;

/// <summary>
/// The exceptions after which a process can no longer be trusted to work: out of memory, an exhausted stack, a native
/// fault. Code that turns every failure into a value (a catch-all at a boundary, such as the activation service) lets
/// them through, so that the process ends instead of going on in a broken state. The application's own policy adds the
/// failures of its UI framework to these.
/// </summary>
public static class CriticalExceptions
{
    /// <summary>True for <paramref name="exception"/>, or an exception it wraps, of a critical type.</summary>
    public static bool IsCritical(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (IsCriticalType(e))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True for an exception that is itself of a critical type (what it wraps is not looked at).</summary>
    public static bool IsCriticalType(Exception exception) =>
        exception is OutOfMemoryException or InsufficientExecutionStackException or StackOverflowException
            or AccessViolationException or SEHException;
}
