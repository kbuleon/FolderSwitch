using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Tests.Hotkeys;

public class HotkeyRulesTests
{
    private static readonly Dictionary<string, string> French = new()
    {
        ["Key.Ctrl"] = "Ctrl",
        ["Key.Alt"] = "Alt",
        ["Key.Shift"] = "Maj",
        ["Key.Win"] = "⊞ Win",
        ["Key.Space"] = "Espace",
    };

    private static string Fr(string key) => French[key];

    private const KeyModifiers CtrlWin = KeyModifiers.Ctrl | KeyModifiers.Win;
    private const KeyModifiers CtrlShift = KeyModifiers.Ctrl | KeyModifiers.Shift;
    private const KeyModifiers CtrlAlt = KeyModifiers.Ctrl | KeyModifiers.Alt;
    private const KeyModifiers WinShift = KeyModifiers.Win | KeyModifiers.Shift;

    private const int F = 0x46;

    // ---- Validate: simple cases from the brief ----

    [Theory]
    [InlineData(KeyModifiers.Ctrl, null, new[] { HotkeyIssue.SingleModifier })]
    [InlineData(KeyModifiers.Alt, null, new[] { HotkeyIssue.SingleModifier })]
    [InlineData(KeyModifiers.Shift, null, new[] { HotkeyIssue.SingleModifier })]
    [InlineData(KeyModifiers.Win, null, new[] { HotkeyIssue.SingleModifier })]
    [InlineData(KeyModifiers.None, null, new[] { HotkeyIssue.NoModifier })]
    [InlineData(KeyModifiers.None, F, new[] { HotkeyIssue.NoModifier })]
    [InlineData(KeyModifiers.None, VirtualKeys.F1, new[] { HotkeyIssue.NoModifier })]
    [InlineData(KeyModifiers.Win, VirtualKeys.L, new[] { HotkeyIssue.ReservedByWindows })]
    [InlineData(CtrlAlt, F, new[] { HotkeyIssue.CtrlAltLikeAltGr })]
    [InlineData(CtrlWin, null, new HotkeyIssue[0])]
    [InlineData(CtrlShift, F, new HotkeyIssue[0])]
    public void Validate_returns_the_expected_issues(KeyModifiers modifiers, int? virtualKey, HotkeyIssue[] expected)
    {
        var issues = HotkeyRules.Validate(new Hotkey(modifiers, virtualKey));

        Assert.Equal(expected, issues);
    }

    // ---- Validate: the full reserved list ----

    [Theory]
    [InlineData(KeyModifiers.Win, VirtualKeys.L)]
    [InlineData(KeyModifiers.Win, VirtualKeys.D)]
    [InlineData(KeyModifiers.Win, VirtualKeys.E)]
    [InlineData(KeyModifiers.Win, VirtualKeys.R)]
    [InlineData(KeyModifiers.Win, VirtualKeys.Tab)]
    [InlineData(KeyModifiers.Win, VirtualKeys.S)]
    [InlineData(WinShift, VirtualKeys.S)]
    [InlineData(CtrlShift, VirtualKeys.Escape)]
    [InlineData(KeyModifiers.Alt, VirtualKeys.Tab)]
    [InlineData(KeyModifiers.Alt, VirtualKeys.F4)]
    public void Reserved_combinations_are_reported_and_blocking(KeyModifiers modifiers, int virtualKey)
    {
        var issues = HotkeyRules.Validate(new Hotkey(modifiers, virtualKey));

        Assert.Equal([HotkeyIssue.ReservedByWindows], issues);
        Assert.All(issues, i => Assert.True(HotkeyRules.IsBlocking(i)));
    }

    [Fact]
    public void Ctrl_Alt_Delete_is_reserved_and_also_warns_about_AltGr()
    {
        var issues = HotkeyRules.Validate(new Hotkey(CtrlAlt, VirtualKeys.Delete));

        Assert.Equal([HotkeyIssue.ReservedByWindows, HotkeyIssue.CtrlAltLikeAltGr], issues);
    }

    [Theory]
    [InlineData(KeyModifiers.Win, F)]                                      // Win+F
    [InlineData(WinShift, VirtualKeys.L)]                                  // Win+Shift+L: only Win+L is reserved
    [InlineData(CtrlWin, VirtualKeys.D)]                                   // modifiers are compared exactly
    [InlineData(KeyModifiers.Shift, VirtualKeys.S)]                        // Shift+S
    [InlineData(KeyModifiers.Ctrl, VirtualKeys.Tab)]                       // Ctrl+Tab
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift, VirtualKeys.Escape)]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Shift, VirtualKeys.F4)]    // Alt+Shift+F4
    [InlineData(KeyModifiers.Ctrl, VirtualKeys.Delete)]                    // Ctrl+Delete
    public void Near_misses_of_reserved_combinations_are_not_reserved(KeyModifiers modifiers, int virtualKey)
    {
        var issues = HotkeyRules.Validate(new Hotkey(modifiers, virtualKey));

        Assert.DoesNotContain(HotkeyIssue.ReservedByWindows, issues);
    }

    // ---- Validate: AltGr warning ----

    [Theory]
    [InlineData(CtrlAlt, null)]
    [InlineData(CtrlAlt | KeyModifiers.Shift, F)]
    [InlineData(CtrlAlt | KeyModifiers.Win, F)]
    [InlineData(CtrlAlt, F)]
    public void Ctrl_plus_Alt_warns_with_or_without_other_modifiers(KeyModifiers modifiers, int? virtualKey)
    {
        var issues = HotkeyRules.Validate(new Hotkey(modifiers, virtualKey));

        Assert.Contains(HotkeyIssue.CtrlAltLikeAltGr, issues);
        Assert.DoesNotContain(issues, HotkeyRules.IsBlocking);
    }

    [Theory]
    [InlineData(KeyModifiers.Ctrl, F)]
    [InlineData(KeyModifiers.Alt, F)]
    [InlineData(CtrlShift, F)]
    public void Without_both_Ctrl_and_Alt_there_is_no_AltGr_warning(KeyModifiers modifiers, int virtualKey)
    {
        var issues = HotkeyRules.Validate(new Hotkey(modifiers, virtualKey));

        Assert.DoesNotContain(HotkeyIssue.CtrlAltLikeAltGr, issues);
    }

    [Fact]
    public void Default_hotkey_has_no_issue()
    {
        Assert.Empty(HotkeyRules.Validate(Hotkey.Default));
    }

    [Fact]
    public void Validate_is_stable_and_never_reports_TakenByOtherApp()
    {
        var all = new List<HotkeyIssue>();
        for (var mods = 0; mods < 16; mods++)
        {
            foreach (int? vk in new int?[] { null, F, VirtualKeys.L, VirtualKeys.Delete, VirtualKeys.F4 })
            {
                var hotkey = new Hotkey((KeyModifiers)mods, vk);
                var first = HotkeyRules.Validate(hotkey);
                var second = HotkeyRules.Validate(hotkey);
                Assert.Equal(first, second);
                all.AddRange(first);
            }
        }

        Assert.DoesNotContain(HotkeyIssue.TakenByOtherApp, all);
    }

    // ---- IsBlocking ----

    [Theory]
    [InlineData(HotkeyIssue.SingleModifier, true)]
    [InlineData(HotkeyIssue.NoModifier, true)]
    [InlineData(HotkeyIssue.ReservedByWindows, true)]
    [InlineData(HotkeyIssue.TakenByOtherApp, true)]
    [InlineData(HotkeyIssue.CtrlAltLikeAltGr, false)]
    public void IsBlocking_matches_the_brief(HotkeyIssue issue, bool expected)
    {
        Assert.Equal(expected, HotkeyRules.IsBlocking(issue));
    }

    // ---- Format ----

    [Fact]
    public void Format_french()
    {
        Assert.Equal("Ctrl + ⊞ Win", HotkeyRules.Format(new Hotkey(CtrlWin, null), Fr));
        Assert.Equal("Ctrl + Maj + F", HotkeyRules.Format(new Hotkey(CtrlShift, F), Fr));
    }

    [Fact]
    public void Format_orders_modifiers_Ctrl_Alt_Shift_Win()
    {
        var all = KeyModifiers.Win | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Ctrl;

        Assert.Equal("Ctrl + Alt + Maj + ⊞ Win", HotkeyRules.Format(new Hotkey(all, null), Fr));
        Assert.Equal("Ctrl + Alt + Maj + ⊞ Win + Q", HotkeyRules.Format(new Hotkey(all, 0x51), Fr));
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x39, "9")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x87, "F24")]
    [InlineData(0x20, "Espace")]
    [InlineData(0x88, "0x88")]
    [InlineData(0xBA, "0xBA")]
    public void Format_renders_the_regular_key(int virtualKey, string expectedKey)
    {
        Assert.Equal("Ctrl + " + expectedKey, HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, virtualKey), Fr));
    }

    // ---- Key names: the keyboard layout's, else the fallback ----

    [Theory]
    [InlineData(0x1B, "Esc")]
    [InlineData(0x09, "Tab")]
    [InlineData(0x08, "Backspace")]
    [InlineData(0x0D, "Enter")]
    [InlineData(0x24, "Home")]
    [InlineData(0x23, "End")]
    [InlineData(0x21, "Page Up")]
    [InlineData(0x22, "Page Down")]
    [InlineData(0x2D, "Insert")]
    [InlineData(0x2E, "Delete")]
    [InlineData(0x25, "←")]
    [InlineData(0x26, "↑")]
    [InlineData(0x27, "→")]
    [InlineData(0x28, "↓")]
    [InlineData(0x60, "Num 0")]
    [InlineData(0x65, "Num 5")]
    [InlineData(0x69, "Num 9")]
    [InlineData(0x6A, "Num *")]
    [InlineData(0x6F, "Num /")]
    [InlineData(0x13, "Pause")]
    [InlineData(0x2C, "Print Screen")]
    public void Without_key_names_a_named_key_gets_its_key_cap_name(int virtualKey, string expectedKey)
    {
        Assert.Equal(expectedKey, HotkeyRules.FallbackKeyName(virtualKey));
        Assert.Equal("Ctrl + Alt + " + expectedKey, HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, virtualKey), Fr));
    }

    [Theory]
    [InlineData(0xBF)] // "/" on a US keyboard, another character elsewhere
    [InlineData(0xBA)]
    [InlineData(0xE2)]
    [InlineData(0x88)]
    public void A_key_whose_character_depends_on_the_layout_has_no_fallback_name(int virtualKey)
    {
        Assert.Null(HotkeyRules.FallbackKeyName(virtualKey));
        Assert.Equal(
            "Ctrl + 0x" + virtualKey.ToString("X2", System.Globalization.CultureInfo.InvariantCulture),
            HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, virtualKey), Fr, new FakeKeyNames()));
    }

    [Fact]
    public void The_layout_name_of_a_key_wins_over_the_fallback()
    {
        var names = new FakeKeyNames { [0x24] = "Origine", [0xBF] = ":" };

        Assert.Equal("Ctrl + Alt + Origine", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 0x24), Fr, names));
        Assert.Equal("Ctrl + :", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0xBF), Fr, names));
    }

    [Fact]
    public void Letters_digits_function_keys_and_space_never_ask_the_layout()
    {
        var names = new FakeKeyNames { [0x41] = "Q", [0x31] = "&", [0x70] = "Aide", [0x20] = "BARRE" };

        Assert.Equal("Ctrl + A", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0x41), Fr, names));
        Assert.Equal("Ctrl + 1", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0x31), Fr, names));
        Assert.Equal("Ctrl + F1", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0x70), Fr, names));
        Assert.Equal("Ctrl + Espace", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0x20), Fr, names));
        Assert.Empty(names.Asked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_layout_name_falls_back(string? layoutName)
    {
        var names = new FakeKeyNames { [0x24] = layoutName };

        Assert.Equal("Ctrl + Home", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0x24), Fr, names));
    }

    [Fact]
    public void Key_names_that_throw_fall_back()
    {
        var names = new FakeKeyNames { Throws = true };

        Assert.Equal("Ctrl + Home", HotkeyRules.Format(new Hotkey(KeyModifiers.Ctrl, 0x24), Fr, names));
    }

    private sealed class FakeKeyNames : IKeyNames
    {
        private readonly Dictionary<int, string?> _names = [];

        public List<int> Asked { get; } = [];

        public bool Throws { get; init; }

        public string? this[int virtualKey]
        {
            set => _names[virtualKey] = value;
        }

        public string? NameOf(int virtualKey)
        {
            Asked.Add(virtualKey);
            return Throws ? throw new InvalidOperationException("layout unavailable") : _names.GetValueOrDefault(virtualKey);
        }
    }

    [Fact]
    public void Format_uses_the_localizer_function_for_every_label()
    {
        var calls = new List<string>();
        string Spy(string key)
        {
            calls.Add(key);
            return "<" + key + ">";
        }

        var text = HotkeyRules.Format(
            new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Win, 0x20), Spy);

        Assert.Equal("<Key.Ctrl> + <Key.Alt> + <Key.Shift> + <Key.Win> + <Key.Space>", text);
        Assert.Equal(["Key.Ctrl", "Key.Alt", "Key.Shift", "Key.Win", "Key.Space"], calls);
    }

    [Fact]
    public void Format_without_any_key_is_empty()
    {
        Assert.Equal("", HotkeyRules.Format(new Hotkey(KeyModifiers.None, null), Fr));
    }

    [Fact]
    public void Format_of_a_key_without_modifier_is_the_key_alone()
    {
        Assert.Equal("F5", HotkeyRules.Format(new Hotkey(KeyModifiers.None, 0x74), Fr));
    }

    [Fact]
    public void Format_default_hotkey()
    {
        Assert.Equal("Ctrl + ⊞ Win", HotkeyRules.Format(Hotkey.Default, Fr));
    }
}
