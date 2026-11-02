// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Hotkeys;

public class VirtualKeysTests
{
    [Fact]
    public void Constants_match_the_Win32_values()
    {
        Assert.Equal(0xA2, VirtualKeys.LControl);
        Assert.Equal(0xA3, VirtualKeys.RControl);
        Assert.Equal(0x11, VirtualKeys.Control);
        Assert.Equal(0x5B, VirtualKeys.LWin);
        Assert.Equal(0x5C, VirtualKeys.RWin);
        Assert.Equal(0xA4, VirtualKeys.LMenu);
        Assert.Equal(0xA5, VirtualKeys.RMenu);
        Assert.Equal(0x12, VirtualKeys.Menu);
        Assert.Equal(0xA0, VirtualKeys.LShift);
        Assert.Equal(0xA1, VirtualKeys.RShift);
        Assert.Equal(0x10, VirtualKeys.Shift);
        Assert.Equal(0x1B, VirtualKeys.Escape);
        Assert.Equal(0x09, VirtualKeys.Tab);
        Assert.Equal(0x2E, VirtualKeys.Delete);
    }

    [Theory]
    [InlineData(VirtualKeys.LControl, KeyModifiers.Ctrl)]
    [InlineData(VirtualKeys.RControl, KeyModifiers.Ctrl)]
    [InlineData(VirtualKeys.Control, KeyModifiers.Ctrl)]
    [InlineData(VirtualKeys.LMenu, KeyModifiers.Alt)]
    [InlineData(VirtualKeys.RMenu, KeyModifiers.Alt)]
    [InlineData(VirtualKeys.Menu, KeyModifiers.Alt)]
    [InlineData(VirtualKeys.LShift, KeyModifiers.Shift)]
    [InlineData(VirtualKeys.RShift, KeyModifiers.Shift)]
    [InlineData(VirtualKeys.Shift, KeyModifiers.Shift)]
    [InlineData(VirtualKeys.LWin, KeyModifiers.Win)]
    [InlineData(VirtualKeys.RWin, KeyModifiers.Win)]
    public void ModifierOf_maps_left_right_and_generic_variants_to_one_flag(int vk, KeyModifiers expected)
    {
        Assert.Equal(expected, VirtualKeys.ModifierOf(vk));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x44)]
    [InlineData(VirtualKeys.Escape)]
    [InlineData(VirtualKeys.Tab)]
    [InlineData(VirtualKeys.Delete)]
    [InlineData(0x5D)] // Apps key, next to the Win keys
    [InlineData(0xFF)]
    [InlineData(-1)]
    public void ModifierOf_returns_null_for_non_modifier_keys(int vk)
    {
        Assert.Null(VirtualKeys.ModifierOf(vk));
    }
}
