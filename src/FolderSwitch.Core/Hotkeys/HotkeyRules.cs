using System.Globalization;
using FolderSwitch.Core.Diagnostics;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Hotkeys;

/// <summary>Static validation and display rules for a <see cref="Hotkey"/>.</summary>
public static class HotkeyRules
{
    private const KeyModifiers AllModifiers =
        KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Win;

    private const KeyModifiers CtrlAlt = KeyModifiers.Ctrl | KeyModifiers.Alt;

    /// <summary>Combinations Windows handles itself. Modifiers and key are both compared exactly.</summary>
    private static readonly (KeyModifiers Modifiers, int VirtualKey)[] Reserved =
    [
        (KeyModifiers.Win, VirtualKeys.L),
        (KeyModifiers.Win, VirtualKeys.D),
        (KeyModifiers.Win, VirtualKeys.E),
        (KeyModifiers.Win, VirtualKeys.R),
        (KeyModifiers.Win, VirtualKeys.Tab),
        (KeyModifiers.Win, VirtualKeys.S),
        (KeyModifiers.Win | KeyModifiers.Shift, VirtualKeys.S),
        (KeyModifiers.Ctrl | KeyModifiers.Shift, VirtualKeys.Escape),
        (KeyModifiers.Alt, VirtualKeys.Tab),
        (KeyModifiers.Alt, VirtualKeys.F4),
        (CtrlAlt, VirtualKeys.Delete),
    ];

    /// <summary>
    /// Static checks only; <see cref="HotkeyIssue.TakenByOtherApp"/> can only be known when the
    /// registration is attempted, so it is never returned here. Issues come in a stable order:
    /// modifier problem, then reserved combination, then AltGr warning.
    /// </summary>
    public static IReadOnlyList<HotkeyIssue> Validate(Hotkey h)
    {
        ArgumentNullException.ThrowIfNull(h);

        var modifiers = h.Modifiers & AllModifiers;
        var issues = new List<HotkeyIssue>(2);

        if (modifiers == KeyModifiers.None)
        {
            issues.Add(HotkeyIssue.NoModifier);
        }
        else if (h.VirtualKey is null && IsSingleFlag(modifiers))
        {
            issues.Add(HotkeyIssue.SingleModifier);
        }

        if (h.VirtualKey is { } vk && IsReserved(modifiers, vk))
        {
            issues.Add(HotkeyIssue.ReservedByWindows);
        }

        if ((modifiers & CtrlAlt) == CtrlAlt)
        {
            issues.Add(HotkeyIssue.CtrlAltLikeAltGr);
        }

        return issues;
    }

    /// <summary>Blocking issues forbid the hotkey; the others are only warnings.</summary>
    public static bool IsBlocking(HotkeyIssue i) => i switch
    {
        HotkeyIssue.SingleModifier or HotkeyIssue.NoModifier
            or HotkeyIssue.ReservedByWindows or HotkeyIssue.TakenByOtherApp => true,
        _ => false,
    };

    /// <summary>
    /// Display text, e.g. <c>Ctrl + Shift + F</c>: modifiers in the order Ctrl, Alt, Shift, Win
    /// (labels from <paramref name="localize"/> with <c>Key.*</c> keys), then the regular key if any.
    /// Letters and digits are shown as their character, F1-F24 as such, space through
    /// <c>Key.Space</c>. Any other key gets the name the keyboard layout gives it
    /// (<paramref name="keyNames"/>, when there is one and it names the key), else the name on a key
    /// cap of a standard keyboard (<c>Home</c>, <c>Page Up</c>, <c>Num 5</c>...: see
    /// <see cref="FallbackKeyName"/>), else <c>0x</c> and two upper-case hex digits.
    /// </summary>
    public static string Format(Hotkey h, Func<string, string> localize, IKeyNames? keyNames = null)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(localize);

        var parts = new List<string>(5);
        if (h.Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add(localize("Key.Ctrl"));
        if (h.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add(localize("Key.Alt"));
        if (h.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add(localize("Key.Shift"));
        if (h.Modifiers.HasFlag(KeyModifiers.Win)) parts.Add(localize("Key.Win"));

        if (h.VirtualKey is { } vk)
        {
            parts.Add(FormatKey(vk, localize, keyNames));
        }

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// The name of a key that is not a letter, a digit, a function key or the space bar, as written on
    /// the key cap of a standard US keyboard; <c>null</c> for a key that has none there, or whose
    /// character depends on the keyboard layout (punctuation).
    /// </summary>
    public static string? FallbackKeyName(int vk) => vk switch
    {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x14 => "Caps Lock",
        0x1B => "Esc",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        0x5D => "Menu",
        >= 0x60 and <= 0x69 => "Num " + (vk - 0x60).ToString(CultureInfo.InvariantCulture),
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        0x90 => "Num Lock",
        0x91 => "Scroll Lock",
        _ => null,
    };

    private static string FormatKey(int vk, Func<string, string> localize, IKeyNames? keyNames)
    {
        if (vk is >= VirtualKeys.A and <= VirtualKeys.Z or >= VirtualKeys.Digit0 and <= VirtualKeys.Digit9)
        {
            return ((char)vk).ToString();
        }

        if (vk is >= VirtualKeys.F1 and <= VirtualKeys.F24)
        {
            return "F" + (vk - VirtualKeys.F1 + 1).ToString(CultureInfo.InvariantCulture);
        }

        if (vk == VirtualKeys.Space)
        {
            return localize("Key.Space");
        }

        string? named = null;
        try
        {
            named = keyNames?.NameOf(vk);
        }
        catch (Exception e) when (!CriticalExceptions.IsCritical(e))
        {
            // The layout's name is a nicety: the fallback below still names the key.
        }

        return !string.IsNullOrWhiteSpace(named)
            ? named.Trim()
            : FallbackKeyName(vk) ?? "0x" + vk.ToString("X2", CultureInfo.InvariantCulture);
    }

    private static bool IsSingleFlag(KeyModifiers modifiers) =>
        modifiers != KeyModifiers.None && ((int)modifiers & ((int)modifiers - 1)) == 0;

    private static bool IsReserved(KeyModifiers modifiers, int vk)
    {
        foreach (var entry in Reserved)
        {
            if (entry.Modifiers == modifiers && entry.VirtualKey == vk)
            {
                return true;
            }
        }

        return false;
    }
}
