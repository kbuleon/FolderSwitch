// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Services;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace FolderSwitch.Windows.Hotkeys;

/// <summary>
/// <see cref="IKeyNames"/> from the keyboard layout of the calling thread: <c>MapVirtualKey</c> gives the
/// key's scan code, <c>GetKeyNameText</c> the name the layout gives that key (« Home », « Origine », « Pos1 »,
/// « / »...). A name the layout writes in capitals (« ORIGINE », « PG.SUIV ») is shown with only its first
/// letter in capitals, like the names of the modifier keys.
/// </summary>
/// <remarks>Any thread. No message, no I/O. Never throws: <c>null</c> when the layout has no name for the key.</remarks>
public sealed class KeyNames : IKeyNames
{
    private const int MaxChars = 64;

    /// <summary>The scan code of an extended key carries this prefix (<c>MAPVK_VK_TO_VSC_EX</c>).</summary>
    private const uint ExtendedPrefix = 0xE000;

    private KeyNames()
    {
    }

    public static KeyNames Instance { get; } = new();

    /// <inheritdoc/>
    public string? NameOf(int virtualKey)
    {
        try
        {
            var scanCode = PInvoke.MapVirtualKey((uint)virtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
            if (scanCode == 0)
            {
                return null;
            }

            // GetKeyNameText takes the key as a WM_KEYDOWN lParam: scan code in bits 16-23, extended flag in bit 24.
            // Some keys of the navigation block share their scan code with the numeric keypad: only the flag tells.
            var extended = (scanCode & 0xFF00) == ExtendedPrefix || IsExtendedKey(virtualKey);
            var lParam = (int)(((scanCode & 0xFF) << 16) | (extended ? 1u << 24 : 0));

            Span<char> buffer = stackalloc char[MaxChars];
            var length = PInvoke.GetKeyNameText(lParam, buffer);
            return length > 0 ? Readable(buffer[..length].ToString()) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The keys Windows marks as extended, whatever the scan code it reports for them.</summary>
    private static bool IsExtendedKey(int vk) => vk is
        0x21 or 0x22 or 0x23 or 0x24 // Page Up, Page Down, End, Home
        or 0x25 or 0x26 or 0x27 or 0x28 // arrows
        or 0x2D or 0x2E // Insert, Delete
        or 0x6F // numeric keypad /
        or 0x90 // Num Lock
        or 0x5B or 0x5C or 0x5D; // Windows keys, application key

    /// <summary>« ORIGINE » becomes « Origine »; « Home », « / » and « F » are kept as they are.</summary>
    private static string? Readable(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var hasLetters = false;
        foreach (var c in trimmed)
        {
            if (char.IsLower(c))
            {
                return trimmed;
            }

            hasLetters |= char.IsUpper(c);
        }

        return hasLetters && trimmed.Length > 1
            ? char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant()
            : trimmed;
    }
}
