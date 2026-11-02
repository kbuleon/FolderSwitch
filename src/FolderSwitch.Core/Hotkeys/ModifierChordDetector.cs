// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Hotkeys;

/// <summary>
/// Detects a modifier-only chord (the default Ctrl + Win): all the keys of <paramref name="target"/>
/// pressed together, then everything released, with no other key pressed in between. Pure logic:
/// the keyboard hook feeds it key events and acts on the returned <see cref="ChordStep"/>.
/// </summary>
/// <remarks>
/// A sequence runs from the first key press (no key down before it) to the release of the last key
/// down. It is poisoned as soon as a key outside the target is pressed, including a modifier that
/// is not part of the target, or when it starts with such a key. Left, right and generic variants of
/// a modifier are the same target key. Keys are tracked by virtual-key code, so an auto-repeat is
/// ignored even if the caller does not flag it. Not thread-safe: call it from the hook thread.
/// An empty target never triggers.
/// </remarks>
public sealed class ModifierChordDetector(KeyModifiers target)
{
    private readonly HashSet<int> _down = [];
    private bool _poisoned;
    private bool _reachedTarget;

    public ChordStep OnKeyDown(int vk, bool isRepeat)
    {
        if (isRepeat || !_down.Add(vk))
        {
            return default;
        }

        if (_down.Count == 1)
        {
            // First key of a new sequence: it only counts if it belongs to the target.
            _poisoned = !IsTargetKey(vk);
            _reachedTarget = false;
        }
        else if (!IsTargetKey(vk))
        {
            _poisoned = true;
        }

        if (!_poisoned && HeldTargetModifiers() == target)
        {
            _reachedTarget = true;
        }

        return default;
    }

    public ChordStep OnKeyUp(int vk)
    {
        if (!_down.Remove(vk))
        {
            return default;
        }

        var clean = !_poisoned && _reachedTarget;
        var suppressStartMenu = clean && VirtualKeys.ModifierOf(vk) == KeyModifiers.Win;
        var triggered = clean && _down.Count == 0;

        if (_down.Count == 0)
        {
            _poisoned = false;
            _reachedTarget = false;
        }

        return new ChordStep(triggered, suppressStartMenu);
    }

    /// <summary>Forgets every key in flight, e.g. when the hook was reinstalled or key events may have been missed.</summary>
    public void Reset()
    {
        _down.Clear();
        _poisoned = false;
        _reachedTarget = false;
    }

    private bool IsTargetKey(int vk) =>
        VirtualKeys.ModifierOf(vk) is { } modifier && (modifier & target) != KeyModifiers.None;

    private KeyModifiers HeldTargetModifiers()
    {
        var held = KeyModifiers.None;
        foreach (var vk in _down)
        {
            if (VirtualKeys.ModifierOf(vk) is { } modifier)
            {
                held |= modifier;
            }
        }

        return held & target;
    }
}
