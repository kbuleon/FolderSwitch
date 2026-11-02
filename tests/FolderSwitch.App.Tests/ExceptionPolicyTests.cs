// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.App.Diagnostics;

namespace FolderSwitch.App.Tests;

/// <summary>Which exceptions end the resident process instead of being survived on the UI thread.</summary>
public class ExceptionPolicyTests
{
    /// <summary>UCEERR_RENDERTHREADFAILURE: WPF's render thread failed, nothing is drawn again.</summary>
    private const int RenderThreadFailure = unchecked((int)0x88980406);

    public static TheoryData<Exception> Critical() =>
    [
        new OutOfMemoryException(),
        new InsufficientExecutionStackException(),
        new AccessViolationException(),
        new SEHException(),
        new COMException("render thread failure", RenderThreadFailure),
        new ExternalException("composition", unchecked((int)0x88980001)),
    ];

    public static TheoryData<Exception> Ordinary() =>
    [
        new InvalidOperationException("an ordinary bug"),
        new NullReferenceException(),
        new IOException("disk full"),
        new COMException("RPC server unavailable", unchecked((int)0x800706BA)),
        new COMException("another facility", unchecked((int)0x88990001)),
        new ArgumentException("bad value"),
    ];

    [Theory]
    [MemberData(nameof(Critical))]
    public void Critical_exceptions_are_critical(Exception exception)
    {
        Assert.True(ExceptionPolicy.IsCritical(exception));
    }

    [Theory]
    [MemberData(nameof(Ordinary))]
    public void Ordinary_exceptions_are_survived(Exception exception)
    {
        Assert.False(ExceptionPolicy.IsCritical(exception));
    }

    [Fact]
    public void A_critical_exception_wrapped_in_ordinary_ones_is_critical()
    {
        var wrapped = new InvalidOperationException("outer", new AggregateException(new OutOfMemoryException()));

        Assert.True(ExceptionPolicy.IsCritical(wrapped));
    }

    [Fact]
    public void A_render_failure_wrapped_in_a_dispatcher_exception_is_critical()
    {
        var wrapped = new InvalidOperationException("dispatcher", new COMException("render", RenderThreadFailure));

        Assert.True(ExceptionPolicy.IsCritical(wrapped));
    }

    [Fact]
    public void The_zombie_render_partition_is_critical()
    {
        // MediaContext raises an InvalidOperationException from NotifyPartitionIsZombie: only that site counts.
        var zombie = Record.Exception(NotifyPartitionIsZombie);
        var elsewhere = Record.Exception(SomewhereElse);

        Assert.IsType<InvalidOperationException>(zombie);
        Assert.True(ExceptionPolicy.IsCritical(zombie));
        Assert.False(ExceptionPolicy.IsCritical(elsewhere));
    }

    [Fact]
    public void Null_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionPolicy.IsCritical(null!));
    }

    private static void NotifyPartitionIsZombie() => throw new InvalidOperationException("render partition is a zombie");

    private static void SomewhereElse() => throw new InvalidOperationException("an ordinary bug");
}
