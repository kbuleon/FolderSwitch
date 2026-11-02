// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.Concurrent;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;
using FolderSwitch.Windows.Hotkeys;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Runs on the real desktop session: <c>RegisterHotKey</c> and the <c>WH_KEYBOARD_LL</c> hook are real.
/// No keystroke is ever sent: key events are fed through the service's internal seam, with a fake
/// keyboard (the mask key is counted, not injected) and the real input ignored, so the user's typing
/// neither disturbs the tests nor gets swallowed. Every registration is released by disposing the
/// services.
/// </summary>
[Trait("Category", "Desktop")]
public sealed class HotkeyServiceTests
{
    private const KeyModifiers CtrlAltShift = KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift;

    // Unusual combinations, unlikely to be taken on the desktop running the tests.
    private static readonly Hotkey CtrlAltShiftF12 = new(CtrlAltShift, VirtualKeys.F1 + 11);
    private static readonly Hotkey CtrlAltShiftF11 = new(CtrlAltShift, VirtualKeys.F1 + 10);

    private const int F = 0x46;
    private const int K = 0x4B;

    private const uint WtsSessionLock = 7;
    private const uint WtsSessionUnlock = 8;

    // ---- Brief tests ----

    [Fact]
    public void Probe_detects_combination_taken_by_another_registration()
    {
        using var sut = new HotkeyService(null);
        using (var holder = new HotkeyService(null))
        {
            RegisterOrFail(holder, CtrlAltShiftF12);

            Assert.False(sut.Probe(CtrlAltShiftF12));
        }

        // Disposing the holder released the combination.
        Assert.True(sut.Probe(CtrlAltShiftF12));
    }

    [Fact]
    public void Register_regular_combination_succeeds_when_free()
    {
        using var sut = new HotkeyService(null);

        var issues = sut.Register(CtrlAltShiftF12);

        // Only the AltGr warning that Validate gives for any Ctrl + Alt combination.
        Assert.Equal([HotkeyIssue.CtrlAltLikeAltGr], issues);
        using var observer = new HotkeyService(null);
        Assert.False(observer.Probe(CtrlAltShiftF12));
    }

    // ---- Regular combinations ----

    [Fact]
    public void Register_reports_TakenByOtherApp_when_the_combination_is_taken()
    {
        using var holder = new HotkeyService(null);
        RegisterOrFail(holder, CtrlAltShiftF12);
        using var sut = new HotkeyService(null);

        var issues = sut.Register(CtrlAltShiftF12);

        Assert.Equal([HotkeyIssue.CtrlAltLikeAltGr, HotkeyIssue.TakenByOtherApp], issues);
    }

    [Fact]
    public void A_failed_registration_keeps_the_previous_hotkey()
    {
        using var holder = new HotkeyService(null);
        RegisterOrFail(holder, CtrlAltShiftF12);
        using var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF11);

        Assert.Contains(HotkeyIssue.TakenByOtherApp, sut.Register(CtrlAltShiftF12));

        using var observer = new HotkeyService(null);
        Assert.False(observer.Probe(CtrlAltShiftF11));
    }

    [Fact]
    public void Registering_replaces_the_previous_hotkey()
    {
        using var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF11);

        RegisterOrFail(sut, CtrlAltShiftF12);

        using var observer = new HotkeyService(null);
        Assert.True(observer.Probe(CtrlAltShiftF11));
        Assert.False(observer.Probe(CtrlAltShiftF12));
    }

    [Fact]
    public void A_blocking_issue_registers_nothing_and_keeps_the_previous_hotkey()
    {
        using var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF11);

        Assert.Equal([HotkeyIssue.ReservedByWindows], sut.Register(new Hotkey(KeyModifiers.Win, VirtualKeys.L)));
        Assert.Equal([HotkeyIssue.SingleModifier], sut.Register(new Hotkey(KeyModifiers.Ctrl, null)));
        Assert.Equal([HotkeyIssue.NoModifier], sut.Register(new Hotkey(KeyModifiers.None, VirtualKeys.F1 + 11)));

        using var observer = new HotkeyService(null);
        Assert.False(observer.Probe(CtrlAltShiftF11));
    }

    [Fact]
    public void Re_registering_the_same_combination_succeeds()
    {
        using var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF12);

        Assert.DoesNotContain(HotkeyIssue.TakenByOtherApp, sut.Register(CtrlAltShiftF12));
    }

    [Fact]
    public void Unregister_releases_the_combination()
    {
        using var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF12);

        sut.Unregister();

        using var observer = new HotkeyService(null);
        Assert.True(observer.Probe(CtrlAltShiftF12));
    }

    [Fact]
    public void Dispose_releases_the_combination_and_later_calls_throw()
    {
        var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF12);

        sut.Dispose();
        sut.Dispose();

        using var observer = new HotkeyService(null);
        Assert.True(observer.Probe(CtrlAltShiftF12));
        Assert.Throws<ObjectDisposedException>(() => sut.Register(CtrlAltShiftF12));
        Assert.Throws<ObjectDisposedException>(() => sut.Probe(CtrlAltShiftF12));
        Assert.Throws<ObjectDisposedException>(sut.Unregister);
        Assert.Throws<ObjectDisposedException>(() => sut.BeginCapture(_ => { }));
    }

    [Fact]
    public void Probe_of_a_free_combination_leaves_it_free()
    {
        using var sut = new HotkeyService(null);

        Assert.True(sut.Probe(CtrlAltShiftF12));

        using var other = new HotkeyService(null);
        RegisterOrFail(other, CtrlAltShiftF12);
    }

    [Fact]
    public void Probe_of_its_own_registration_is_true()
    {
        using var sut = new HotkeyService(null);
        RegisterOrFail(sut, CtrlAltShiftF12);

        Assert.True(sut.Probe(CtrlAltShiftF12));
    }

    [Fact]
    public void Probe_of_a_modifier_only_hotkey_is_true()
    {
        using var sut = new HotkeyService(null);

        Assert.True(sut.Probe(Hotkey.Default));
    }

    [Fact]
    public void Public_methods_can_be_called_from_several_threads()
    {
        using (var h = new Harness())
        {
            Parallel.For(0, 60, i =>
            {
                switch (i % 4)
                {
                    case 0: h.Service.Register(CtrlAltShiftF11); break;
                    case 1: h.Service.Register(Hotkey.Default); break;
                    case 2: Assert.True(h.Service.Probe(CtrlAltShiftF12)); break;
                    default: h.Service.Unregister(); break;
                }
            });

            h.Service.Unregister();
            Assert.False(h.Service.IsHookInstalled);
        }

        using var observer = new HotkeyService(null);
        Assert.True(observer.Probe(CtrlAltShiftF11));
    }

    // ---- Modifier-only chord ----

    [Fact]
    public void A_modifier_only_hotkey_installs_the_hook_and_Unregister_removes_it()
    {
        using var h = new Harness();
        Assert.False(h.Service.IsHookInstalled);

        Assert.Empty(h.Service.Register(Hotkey.Default));
        Assert.True(h.Service.IsHookInstalled);

        h.Service.Unregister();
        Assert.False(h.Service.IsHookInstalled);
    }

    [Fact]
    public void Replacing_a_modifier_only_hotkey_by_a_regular_one_removes_the_hook()
    {
        using var h = new Harness();
        Assert.Empty(h.Service.Register(Hotkey.Default));

        RegisterOrFail(h.Service, CtrlAltShiftF12);

        Assert.False(h.Service.IsHookInstalled);
    }

    [Fact]
    public void Chord_raises_Triggered_on_the_context_and_masks_the_Win_release()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LWin);
        Assert.Equal(0, h.Keyboard.MaskKeysSent);
        Assert.False(h.Release(VirtualKeys.LWin));
        // Injected while handling the Win release, before letting it through.
        Assert.Equal(1, h.Keyboard.MaskKeysSent);
        Assert.False(h.Release(VirtualKeys.LControl));

        // Posted to the context, never raised from the hook.
        Assert.Equal(0, h.Triggers);
        h.Pump();
        Assert.Equal(1, h.Triggers);
        Assert.Equal(Environment.CurrentManagedThreadId, h.LastTriggerThreadId);
    }

    [Fact]
    public void Chord_released_Ctrl_first_triggers_on_the_Win_release()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        h.Press(VirtualKeys.LWin);
        h.Press(VirtualKeys.RControl);
        h.Release(VirtualKeys.RControl);
        h.Release(VirtualKeys.LWin);
        h.Pump();

        Assert.Equal(1, h.Triggers);
        Assert.Equal(1, h.Keyboard.MaskKeysSent);
    }

    [Fact]
    public void Chord_with_a_third_key_does_not_trigger_nor_mask()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LWin);
        Assert.False(h.Press(VirtualKeys.D));
        h.Release(VirtualKeys.D);
        h.Release(VirtualKeys.LWin);
        h.Release(VirtualKeys.LControl);
        h.Pump();

        Assert.Equal(0, h.Triggers);
        Assert.Equal(0, h.Keyboard.MaskKeysSent);
    }

    [Fact]
    public void Win_alone_is_left_to_Windows()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        Assert.False(h.Press(VirtualKeys.LWin));
        Assert.False(h.Release(VirtualKeys.LWin));
        h.Pump();

        Assert.Equal(0, h.Triggers);
        Assert.Equal(0, h.Keyboard.MaskKeysSent);
    }

    [Fact]
    public void Triggered_already_posted_is_dropped_after_Unregister()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);
        PlayCtrlWin(h);
        h.Service.Flush();

        h.Service.Unregister();
        h.Context.RunPosted();

        Assert.Equal(0, h.Triggers);
    }

    [Fact]
    public void Session_lock_resets_a_chord_in_progress()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LWin);
        h.Service.SimulateSessionChange(WtsSessionLock);
        h.Release(VirtualKeys.LWin);
        h.Release(VirtualKeys.LControl);
        h.Pump();
        Assert.Equal(0, h.Triggers);

        // After the unlock (hook reinstalled), the chord works again.
        h.Service.SimulateSessionChange(WtsSessionUnlock);
        Assert.True(h.Service.IsHookInstalled);
        PlayCtrlWin(h);
        h.Pump();
        Assert.Equal(1, h.Triggers);
    }

    // ---- Keeping the hook alive ----

    [Fact]
    public void The_periodic_re_arm_installs_a_fresh_hook_that_still_triggers()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);
        var installs = h.Service.HookInstallCount;

        h.Service.SimulateRearmTick();

        Assert.Equal(installs + 1, h.Service.HookInstallCount);
        Assert.True(h.Service.IsHookInstalled);
        PlayCtrlWin(h);
        h.Pump();
        Assert.Equal(1, h.Triggers);
    }

    [Fact]
    public void The_periodic_re_arm_waits_while_a_modifier_is_down()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);
        var installs = h.Service.HookInstallCount;

        // In the middle of a chord: put off, and the chord completes as if nothing happened.
        h.Press(VirtualKeys.LControl);
        h.Service.SimulateRearmTick();
        Assert.Equal(installs, h.Service.HookInstallCount);
        h.Press(VirtualKeys.LWin);
        h.Release(VirtualKeys.LWin);
        h.Release(VirtualKeys.LControl);
        h.Pump();
        Assert.Equal(1, h.Triggers);

        // Keys up: done at the next try.
        h.Service.SimulateRearmTick();
        Assert.Equal(installs + 1, h.Service.HookInstallCount);
    }

    [Fact]
    public void The_periodic_re_arm_waits_while_a_capture_runs()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        using (h.BeginCapture())
        {
            var installs = h.Service.HookInstallCount;
            h.Service.SimulateRearmTick();
            Assert.Equal(installs, h.Service.HookInstallCount);
        }

        var after = h.Service.HookInstallCount;
        h.Service.SimulateRearmTick();
        Assert.Equal(after + 1, h.Service.HookInstallCount);
    }

    [Fact]
    public void The_periodic_re_arm_installs_nothing_when_no_hook_is_needed()
    {
        using var h = new Harness();
        RegisterOrFail(h.Service, CtrlAltShiftF12);

        h.Service.SimulateRearmTick();

        Assert.False(h.Service.IsHookInstalled);
        Assert.Equal(0, h.Service.HookInstallCount);
    }

    [Fact]
    public void Resume_from_sleep_reinstalls_the_hook_and_resets_a_chord_in_progress()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);
        var installs = h.Service.HookInstallCount;

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LWin);
        h.Service.SimulateResume();
        h.Release(VirtualKeys.LWin);
        h.Release(VirtualKeys.LControl);
        h.Pump();
        Assert.Equal(0, h.Triggers);
        Assert.Equal(installs + 1, h.Service.HookInstallCount);

        PlayCtrlWin(h);
        h.Pump();
        Assert.Equal(1, h.Triggers);
    }

    [Fact]
    public void Missed_key_releases_do_not_block_the_next_chord()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);

        // Ctrl + Alt + Del: the releases happen on the secure desktop, out of the hook's sight.
        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LMenu);
        h.Keyboard.SetDown(VirtualKeys.LControl, false);
        h.Keyboard.SetDown(VirtualKeys.LMenu, false);

        PlayCtrlWin(h);
        h.Pump();

        Assert.Equal(1, h.Triggers);
    }

    [Fact]
    public async Task Without_a_context_Triggered_is_raised_on_the_hook_thread()
    {
        var keyboard = new FakeKeyboard();
        using var sut = new HotkeyService(null, keyboard, processRealInput: false);
        var raisedOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Triggered += (_, _) => raisedOn.TrySetResult(Environment.CurrentManagedThreadId);
        sut.Register(Hotkey.Default);

        foreach (var (vk, down) in new[]
                 {
                     (VirtualKeys.LControl, true), (VirtualKeys.LWin, true),
                     (VirtualKeys.LWin, false), (VirtualKeys.LControl, false),
                 })
        {
            sut.SimulateKey(vk, down);
            keyboard.SetDown(vk, down);
        }

        var testThread = Environment.CurrentManagedThreadId;
        var hookThread = await raisedOn.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(testThread, hookThread);
    }

    // ---- Capture ----

    [Fact]
    public void Capture_installs_the_hook_even_with_a_regular_hotkey_and_removes_it_after()
    {
        using var h = new Harness();
        RegisterOrFail(h.Service, CtrlAltShiftF12);
        Assert.False(h.Service.IsHookInstalled);

        using var capture = h.BeginCapture();
        Assert.True(h.Service.IsHookInstalled);

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LShift);
        h.Press(F);
        h.Release(F);
        h.Release(VirtualKeys.LShift);
        h.Release(VirtualKeys.LControl);
        h.Pump();

        Assert.Equal([new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, F)], h.Captured);
        Assert.False(h.Service.IsHookInstalled);
    }

    [Fact]
    public void Capture_returns_modifiers_and_regular_key_and_swallows_the_key()
    {
        using var h = new Harness();
        using var capture = h.BeginCapture();

        Assert.False(h.Press(VirtualKeys.LControl));
        Assert.False(h.Press(VirtualKeys.RShift));
        Assert.True(h.Press(F));
        // Auto-repeat and release of the captured key are swallowed too.
        Assert.True(h.Press(F));
        Assert.True(h.Release(F));
        Assert.False(h.Release(VirtualKeys.RShift));
        Assert.False(h.Release(VirtualKeys.LControl));
        h.Pump();

        Assert.Equal([new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, F)], h.Captured);
        Assert.Equal(0, h.Keyboard.MaskKeysSent);
    }

    [Fact]
    public void Capture_of_modifiers_alone_completes_when_all_are_released()
    {
        using var h = new Harness();
        using var capture = h.BeginCapture();

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LWin);
        h.Release(VirtualKeys.LWin);
        h.Pump();
        Assert.Empty(h.Captured);
        // No Start menu while capturing.
        Assert.Equal(1, h.Keyboard.MaskKeysSent);

        h.Release(VirtualKeys.LControl);
        h.Pump();

        Assert.Equal([Hotkey.Default], h.Captured);
    }

    [Fact]
    public void Capture_ignores_a_single_modifier_and_a_key_without_modifier()
    {
        using var h = new Harness();
        using var capture = h.BeginCapture();

        h.Press(VirtualKeys.LShift);
        h.Release(VirtualKeys.LShift);
        // Escape goes through: the capture UI can use it to cancel.
        Assert.False(h.Press(VirtualKeys.Escape));
        Assert.False(h.Release(VirtualKeys.Escape));
        h.Pump();
        Assert.Empty(h.Captured);

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LMenu);
        Assert.True(h.Press(K));
        h.Release(K);
        // Alt released after the swallowed key: masked so the menu bar does not open.
        h.Release(VirtualKeys.LMenu);
        h.Release(VirtualKeys.LControl);
        h.Pump();

        Assert.Equal([new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, K)], h.Captured);
        Assert.Equal(1, h.Keyboard.MaskKeysSent);
    }

    [Fact]
    public void Capture_with_Win_and_a_regular_key_masks_the_Win_release()
    {
        using var h = new Harness();
        using var capture = h.BeginCapture();

        h.Press(VirtualKeys.LWin);
        Assert.True(h.Press(K));
        h.Release(K);
        Assert.Equal(0, h.Keyboard.MaskKeysSent);
        h.Release(VirtualKeys.LWin);
        h.Pump();

        Assert.Equal([new Hotkey(KeyModifiers.Win, K)], h.Captured);
        Assert.Equal(1, h.Keyboard.MaskKeysSent);
        Assert.False(h.Service.IsHookInstalled);
    }

    [Fact]
    public void Capture_takes_the_place_of_the_registered_chord()
    {
        using var h = new Harness();
        h.Service.Register(Hotkey.Default);
        using var capture = h.BeginCapture();

        PlayCtrlWin(h);
        h.Pump();
        Assert.Equal([Hotkey.Default], h.Captured);
        Assert.Equal(0, h.Triggers);

        // Capture ended by itself: the chord triggers again.
        PlayCtrlWin(h);
        h.Pump();
        Assert.Equal(1, h.Triggers);
        Assert.Single(h.Captured);
    }

    [Fact]
    public void Disposing_the_capture_ends_it_without_callback()
    {
        using var h = new Harness();
        var capture = h.BeginCapture();

        capture.Dispose();
        capture.Dispose();

        Assert.False(h.Service.IsHookInstalled);
        h.Service.Register(Hotkey.Default);
        PlayCtrlWin(h);
        h.Pump();
        Assert.Empty(h.Captured);
        Assert.Equal(1, h.Triggers);
    }

    [Fact]
    public void A_capture_cancelled_before_its_callback_runs_delivers_nothing()
    {
        using var h = new Harness();
        var capture = h.BeginCapture();
        PlayCtrlWin(h);
        h.Service.Flush();

        capture.Dispose();
        h.Context.RunPosted();

        Assert.Empty(h.Captured);
    }

    [Fact]
    public void A_new_capture_replaces_the_one_in_progress()
    {
        using var h = new Harness();
        var firstCaptured = new List<Hotkey>();
        using var first = h.Service.BeginCapture(firstCaptured.Add);
        using var second = h.BeginCapture();

        PlayCtrlWin(h);
        h.Pump();

        Assert.Empty(firstCaptured);
        Assert.Equal([Hotkey.Default], h.Captured);
    }

    // ---- Helpers ----

    private static void RegisterOrFail(HotkeyService service, Hotkey hotkey)
    {
        var issues = service.Register(hotkey);
        Assert.False(
            issues.Contains(HotkeyIssue.TakenByOtherApp),
            $"{hotkey} is already taken on this desktop: these tests need it free.");
    }

    private static void PlayCtrlWin(Harness h)
    {
        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LWin);
        h.Release(VirtualKeys.LWin);
        h.Release(VirtualKeys.LControl);
    }

    /// <summary>A service with a queued context and a fake keyboard, fed through the internal seam.</summary>
    private sealed class Harness : IDisposable
    {
        public Harness()
        {
            Service = new HotkeyService(Context, Keyboard, processRealInput: false);
            Service.Triggered += (_, _) =>
            {
                Triggers++;
                LastTriggerThreadId = Environment.CurrentManagedThreadId;
            };
        }

        public QueueContext Context { get; } = new();

        public FakeKeyboard Keyboard { get; } = new();

        public HotkeyService Service { get; }

        public List<Hotkey> Captured { get; } = [];

        public int Triggers { get; private set; }

        public int LastTriggerThreadId { get; private set; }

        public IDisposable BeginCapture() => Service.BeginCapture(Captured.Add);

        /// <returns>Whether the service swallowed the event.</returns>
        public bool Press(int vk)
        {
            // Like the system: the key state changes once the hook let the event through.
            var swallowed = Service.SimulateKey(vk, true);
            Keyboard.SetDown(vk, true);
            return swallowed;
        }

        /// <returns>Whether the service swallowed the event.</returns>
        public bool Release(int vk)
        {
            var swallowed = Service.SimulateKey(vk, false);
            Keyboard.SetDown(vk, false);
            return swallowed;
        }

        /// <summary>Waits for the hook thread to post what it has, then runs it on this thread.</summary>
        public void Pump()
        {
            Service.Flush();
            Context.RunPosted();
        }

        public void Dispose() => Service.Dispose();
    }

    /// <summary>A context whose posted callbacks run only when the test asks.</summary>
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("The hotkey service must never block on the context.");

        public void RunPosted()
        {
            while (_posted.TryDequeue(out var posted))
            {
                posted.Callback(posted.State);
            }
        }
    }

    private sealed class FakeKeyboard : IKeyboardSystem
    {
        private readonly ConcurrentDictionary<int, bool> _down = new();
        private int _maskKeysSent;

        public int MaskKeysSent => Volatile.Read(ref _maskKeysSent);

        public void SendMaskKey() => Interlocked.Increment(ref _maskKeysSent);

        public bool IsKeyDown(int virtualKey) => _down.ContainsKey(virtualKey);

        public void SetDown(int virtualKey, bool down)
        {
            if (down)
            {
                _down[virtualKey] = true;
            }
            else
            {
                _down.TryRemove(virtualKey, out _);
            }
        }
    }
}
