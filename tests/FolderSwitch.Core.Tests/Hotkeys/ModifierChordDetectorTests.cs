// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Hotkeys;

public class ModifierChordDetectorTests
{
    private const KeyModifiers CtrlWin = KeyModifiers.Ctrl | KeyModifiers.Win;

    private static ModifierChordDetector NewCtrlWin() => new(CtrlWin);

    /// <summary>A key event of a scripted sequence: press (with the auto-repeat flag) or release.</summary>
    private readonly record struct Ev(int Vk, bool IsDown, bool IsRepeat = false);

    private static Ev Down(int vk) => new(vk, true);
    private static Ev Repeat(int vk) => new(vk, true, true);
    private static Ev Up(int vk) => new(vk, false);

    private static List<ChordStep> Play(ModifierChordDetector detector, params Ev[] events)
    {
        var steps = new List<ChordStep>(events.Length);
        foreach (var e in events)
        {
            steps.Add(e.IsDown ? detector.OnKeyDown(e.Vk, e.IsRepeat) : detector.OnKeyUp(e.Vk));
        }

        return steps;
    }

    private static readonly ChordStep Nothing = new(false, false);
    private static readonly ChordStep SuppressOnly = new(false, true);
    private static readonly ChordStep TriggerOnly = new(true, false);
    private static readonly ChordStep TriggerAndSuppress = new(true, true);

    private const int D = 0x44;

    // ---- Brief tests ----

    [Fact]
    public void Ctrl_then_Win_released_alone_triggers()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Fact]
    public void Win_then_Ctrl_order_also_triggers()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LWin), Down(VirtualKeys.LControl), Up(VirtualKeys.LControl), Up(VirtualKeys.LWin));

        Assert.Equal([Nothing, Nothing, Nothing, TriggerAndSuppress], steps);
    }

    [Fact]
    public void Third_key_cancels()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Down(D), Up(D), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Extra_modifier_cancels()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Down(VirtualKeys.LShift),
            Up(VirtualKeys.LShift), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Extra_modifier_released_last_still_cancels()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Down(VirtualKeys.LMenu),
            Up(VirtualKeys.LWin), Up(VirtualKeys.LControl), Up(VirtualKeys.LMenu));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Autorepeat_is_ignored()
    {
        var detector = NewCtrlWin();
        var steps = Play(detector,
            Down(VirtualKeys.LControl),
            Repeat(VirtualKeys.LControl), Repeat(VirtualKeys.LControl), Repeat(VirtualKeys.LControl),
            Repeat(VirtualKeys.LControl), Repeat(VirtualKeys.LControl),
            Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.Equal(
            [Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, SuppressOnly, TriggerOnly],
            steps);
    }

    [Fact]
    public void Autorepeat_is_ignored_even_when_the_caller_does_not_flag_it()
    {
        // The low-level hook cannot always tell a repeat from a press: the detector tracks the keys itself.
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LControl), Down(VirtualKeys.LControl),
            Down(VirtualKeys.LWin), Down(VirtualKeys.LWin),
            Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.Equal(TriggerOnly, steps[^1]);
        Assert.Equal(SuppressOnly, steps[^2]);
        Assert.All(steps.Take(5), s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Repeat_of_a_key_that_is_not_down_is_ignored()
    {
        var detector = NewCtrlWin();

        Assert.Equal(Nothing, detector.OnKeyDown(VirtualKeys.LControl, isRepeat: true));
        Assert.Equal(Nothing, detector.OnKeyUp(VirtualKeys.LControl));
    }

    [Fact]
    public void Ctrl_alone_never_triggers()
    {
        var steps = Play(NewCtrlWin(), Down(VirtualKeys.LControl), Up(VirtualKeys.LControl));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Win_alone_never_triggers_and_never_suppresses()
    {
        var steps = Play(NewCtrlWin(), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Left_and_right_variants_are_equivalent()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.RControl), Down(VirtualKeys.RWin), Up(VirtualKeys.RWin), Up(VirtualKeys.RControl));

        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Theory]
    [InlineData(VirtualKeys.LControl, VirtualKeys.LWin)]
    [InlineData(VirtualKeys.LControl, VirtualKeys.RWin)]
    [InlineData(VirtualKeys.RControl, VirtualKeys.LWin)]
    [InlineData(VirtualKeys.Control, VirtualKeys.LWin)]
    [InlineData(VirtualKeys.Control, VirtualKeys.RWin)]
    public void Every_Ctrl_and_Win_variant_combination_triggers(int ctrl, int win)
    {
        var steps = Play(NewCtrlWin(), Down(ctrl), Down(win), Up(win), Up(ctrl));

        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Fact]
    public void Left_and_right_of_the_same_modifier_count_as_one_target_key()
    {
        // LCtrl + RCtrl is still "Ctrl only": the Win part of the target was never pressed.
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.RControl), Up(VirtualKeys.RControl), Up(VirtualKeys.LControl));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    // ---- Sequence boundaries ----

    [Fact]
    public void Releasing_Ctrl_first_while_Win_is_held_does_not_trigger_or_suppress()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LControl), Up(VirtualKeys.LWin));

        Assert.Equal([Nothing, Nothing, Nothing, TriggerAndSuppress], steps);
    }

    [Fact]
    public void Target_keys_never_held_together_do_not_trigger()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Up(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void A_foreign_key_held_before_the_chord_poisons_the_whole_sequence()
    {
        var steps = Play(NewCtrlWin(),
            Down(D), Down(VirtualKeys.LControl), Down(VirtualKeys.LWin),
            Up(VirtualKeys.LWin), Up(VirtualKeys.LControl), Up(D));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Detector_resets_after_a_trigger()
    {
        var detector = NewCtrlWin();
        var chord = new[]
        {
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl),
        };

        var first = Play(detector, chord);
        var second = Play(detector, chord);

        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Detector_resets_after_a_poisoned_sequence_ends()
    {
        var detector = NewCtrlWin();
        Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Down(D),
            Up(D), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        var steps = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Fact]
    public void A_poisoned_sequence_stays_poisoned_after_the_foreign_key_is_released()
    {
        // Ctrl+Win+D, then D released while Ctrl+Win stay down: still not a chord.
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Down(D), Up(D),
            Down(VirtualKeys.RWin), Up(VirtualKeys.RWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Foreign_key_pressed_after_the_target_was_released_still_cancels_the_chord()
    {
        // Ctrl held, Win pressed and released, then D, then Ctrl released: nothing may fire.
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin),
            Down(D), Up(D), Up(VirtualKeys.LControl));

        Assert.Equal(SuppressOnly, steps[2]);        // Win was released alone before D: Start menu masked
        Assert.Equal(Nothing, steps[^1]);            // but the chord is cancelled
        Assert.DoesNotContain(steps, s => s.Triggered);
    }

    // ---- Unseen keys, Reset ----

    [Fact]
    public void Key_up_of_a_key_never_seen_down_is_ignored()
    {
        var detector = NewCtrlWin();

        Assert.Equal(Nothing, detector.OnKeyUp(VirtualKeys.LWin));
        Assert.Equal(Nothing, detector.OnKeyUp(VirtualKeys.LControl));
        Assert.Equal(Nothing, detector.OnKeyUp(D));

        var steps = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));
        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Fact]
    public void Stray_key_up_in_the_middle_of_a_chord_does_not_break_it()
    {
        var steps = Play(NewCtrlWin(),
            Down(VirtualKeys.LControl), Up(D), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));

        Assert.Equal([Nothing, Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Fact]
    public void Reset_forgets_the_keys_in_flight()
    {
        var detector = NewCtrlWin();
        detector.OnKeyDown(VirtualKeys.LControl, false);
        detector.OnKeyDown(VirtualKeys.LWin, false);

        detector.Reset();

        // The releases of the forgotten keys are unseen: they neither trigger nor suppress.
        Assert.Equal(Nothing, detector.OnKeyUp(VirtualKeys.LWin));
        Assert.Equal(Nothing, detector.OnKeyUp(VirtualKeys.LControl));

        var steps = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));
        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    [Fact]
    public void Reset_clears_a_poisoned_sequence()
    {
        var detector = NewCtrlWin();
        Play(detector, Down(D), Down(VirtualKeys.LControl));

        detector.Reset();

        var steps = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin), Up(VirtualKeys.LControl));
        Assert.Equal([Nothing, Nothing, SuppressOnly, TriggerOnly], steps);
    }

    // ---- Other targets ----

    [Fact]
    public void Single_modifier_target_triggers_on_release()
    {
        var steps = Play(new ModifierChordDetector(KeyModifiers.Win), Down(VirtualKeys.LWin), Up(VirtualKeys.LWin));

        Assert.Equal([Nothing, TriggerAndSuppress], steps);
    }

    [Fact]
    public void Target_without_Win_never_suppresses_the_Start_menu()
    {
        var detector = new ModifierChordDetector(KeyModifiers.Ctrl | KeyModifiers.Shift);

        var steps = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.RShift), Up(VirtualKeys.RShift), Up(VirtualKeys.LControl));

        Assert.Equal([Nothing, Nothing, Nothing, TriggerOnly], steps);
    }

    [Fact]
    public void Win_poisons_a_target_that_does_not_include_it()
    {
        var detector = new ModifierChordDetector(KeyModifiers.Ctrl | KeyModifiers.Shift);

        var steps = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LShift), Down(VirtualKeys.LWin),
            Up(VirtualKeys.LWin), Up(VirtualKeys.LShift), Up(VirtualKeys.LControl));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }

    [Fact]
    public void Target_with_three_modifiers_needs_all_of_them()
    {
        var detector = new ModifierChordDetector(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win);

        var incomplete = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LMenu), Up(VirtualKeys.LMenu), Up(VirtualKeys.LControl));
        var complete = Play(detector,
            Down(VirtualKeys.LControl), Down(VirtualKeys.LMenu), Down(VirtualKeys.LWin),
            Up(VirtualKeys.LWin), Up(VirtualKeys.LMenu), Up(VirtualKeys.LControl));

        Assert.All(incomplete, s => Assert.Equal(Nothing, s));
        Assert.Equal([Nothing, Nothing, Nothing, SuppressOnly, Nothing, TriggerOnly], complete);
    }

    [Fact]
    public void Empty_target_never_triggers()
    {
        var steps = Play(new ModifierChordDetector(KeyModifiers.None),
            Down(VirtualKeys.LControl), Up(VirtualKeys.LControl), Down(D), Up(D));

        Assert.All(steps, s => Assert.Equal(Nothing, s));
    }
}
