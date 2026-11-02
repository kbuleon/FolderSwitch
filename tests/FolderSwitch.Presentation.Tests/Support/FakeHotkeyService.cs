// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A hotkey service without a keyboard. <see cref="Press"/> plays the combination the user presses during a
/// capture; combinations listed in <see cref="Taken"/> belong to "another application".
/// </summary>
internal sealed class FakeHotkeyService : IHotkeyService
{
    private Action<Hotkey>? _onCaptured;
    private CaptureHandle? _handle;

    /// <summary>Raised by <see cref="Trigger"/> only.</summary>
    public event EventHandler? Triggered;

    /// <summary>How many handlers listen to <see cref="Triggered"/>.</summary>
    public int TriggeredListeners => Triggered?.GetInvocationList().Length ?? 0;

    /// <summary>How many times <see cref="Unregister"/> was called.</summary>
    public int UnregisterCount { get; private set; }

    public HashSet<Hotkey> Taken { get; } = new();

    /// <summary>When set, replaces the answer of <see cref="Register"/> (nothing is registered then).</summary>
    public Func<Hotkey, IReadOnlyList<HotkeyIssue>>? RegisterAnswer { get; set; }

    /// <summary>When set, thrown by <see cref="Register"/>.</summary>
    public Exception? RegisterThrows { get; set; }

    /// <summary>When set, <see cref="BeginCapture"/> delivers this combination before returning.</summary>
    public Hotkey? CaptureImmediately { get; set; }

    /// <summary>When set, thrown by <see cref="BeginCapture"/> (the keyboard hook could not be installed, say).</summary>
    public Exception? BeginCaptureThrows { get; set; }

    /// <summary>When set, thrown by <see cref="Probe"/>.</summary>
    public Exception? ProbeThrows { get; set; }

    /// <summary>The hotkey registered last; <c>null</c> before any successful registration.</summary>
    public Hotkey? Current { get; private set; }

    public List<Hotkey> RegisterCalls { get; } = new();

    public List<Hotkey> Probed { get; } = new();

    public int CaptureCount { get; private set; }

    /// <summary>True between <see cref="BeginCapture"/> and the capture or the disposal of its handle.</summary>
    public bool IsCapturing => _handle is { Disposed: false } && _onCaptured is not null;

    /// <summary>The handle returned by the last <see cref="BeginCapture"/>.</summary>
    public bool LastHandleDisposed => _handle?.Disposed ?? false;

    /// <summary>The callback of the last capture started, kept after the capture ended.</summary>
    public Action<Hotkey>? LastCallback { get; private set; }

    public IReadOnlyList<HotkeyIssue> Register(Hotkey h)
    {
        RegisterCalls.Add(h);
        if (RegisterThrows is { } exception)
        {
            throw exception;
        }

        if (RegisterAnswer is { } answer)
        {
            return answer(h);
        }

        var issues = HotkeyRules.Validate(h);
        if (issues.Any(HotkeyRules.IsBlocking))
        {
            return issues;
        }

        if (!h.IsModifierOnly && Taken.Contains(h))
        {
            return [.. issues, HotkeyIssue.TakenByOtherApp];
        }

        Current = h;
        return issues;
    }

    public void Unregister()
    {
        UnregisterCount++;
        Current = null;
    }

    /// <summary>The shortcut fires, as the real service raises it for the registered one (the caller checks there is one).</summary>
    public void Trigger() => Triggered?.Invoke(this, EventArgs.Empty);

    public IDisposable BeginCapture(Action<Hotkey> onCaptured)
    {
        CaptureCount++;
        if (BeginCaptureThrows is { } exception)
        {
            throw exception;
        }

        _handle?.Dispose();
        _onCaptured = onCaptured;
        LastCallback = onCaptured;
        var handle = new CaptureHandle();
        _handle = handle;

        if (CaptureImmediately is { } captured)
        {
            Press(captured);
        }

        return handle;
    }

    /// <summary>The user presses a combination: delivered to the capture in progress, which then ends.</summary>
    public void Press(Hotkey h)
    {
        if (!IsCapturing)
        {
            return;
        }

        var callback = _onCaptured!;
        _onCaptured = null;
        callback(h);
    }

    /// <summary>
    /// Like the real service, the combination currently registered counts as free; a modifier-only hotkey
    /// reserves nothing.
    /// </summary>
    public bool Probe(Hotkey h)
    {
        Probed.Add(h);
        if (ProbeThrows is { } exception)
        {
            throw exception;
        }

        return h.IsModifierOnly || h == Current || !Taken.Contains(h);
    }

    public void Dispose()
    {
    }

    private sealed class CaptureHandle : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
