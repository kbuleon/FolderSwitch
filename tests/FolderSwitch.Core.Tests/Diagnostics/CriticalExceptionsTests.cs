using System.Runtime.InteropServices;
using FolderSwitch.Core.Diagnostics;

namespace FolderSwitch.Core.Tests.Diagnostics;

public class CriticalExceptionsTests
{
    public static TheoryData<Exception> Critical() =>
    [
        new OutOfMemoryException(),
        new InsufficientExecutionStackException(),
        new AccessViolationException(),
        new SEHException(),
    ];

    public static TheoryData<Exception> Ordinary() =>
    [
        new InvalidOperationException(),
        new IOException(),
        new COMException("RPC failed", unchecked((int)0x800706BA)),
        new ArgumentException(),
        new OperationCanceledException(),
    ];

    [Theory]
    [MemberData(nameof(Critical))]
    public void Critical_types_are_critical(Exception exception)
    {
        Assert.True(CriticalExceptions.IsCriticalType(exception));
        Assert.True(CriticalExceptions.IsCritical(exception));
    }

    [Theory]
    [MemberData(nameof(Ordinary))]
    public void Ordinary_exceptions_are_not(Exception exception)
    {
        Assert.False(CriticalExceptions.IsCritical(exception));
    }

    [Fact]
    public void A_critical_exception_wrapped_in_another_is_critical()
    {
        var wrapped = new InvalidOperationException("outer", new TargetInvocationWrapper(new OutOfMemoryException()));

        Assert.True(CriticalExceptions.IsCritical(wrapped));
        Assert.False(CriticalExceptions.IsCriticalType(wrapped));
    }

    [Fact]
    public void Null_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => CriticalExceptions.IsCritical(null!));
    }

    private sealed class TargetInvocationWrapper(Exception inner) : Exception("middle", inner);
}
