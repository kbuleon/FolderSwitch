// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.App.Startup;

/// <summary>What a press of the global shortcut does.</summary>
internal enum ShortcutAction
{
    /// <summary>Nothing: a Windows picker of FolderSwitch's is open and must be answered first.</summary>
    None,

    /// <summary>
    /// One of FolderSwitch's own boxes is open (a message, the web address window, the update offer): it comes to the
    /// front and takes the keyboard, wherever it was (behind another application, or shown without the focus).
    /// </summary>
    FrontBox,

    /// <summary>The window is hidden: it is shown for the calling window, in normal use.</summary>
    Show,

    /// <summary>The window is in front: it hides.</summary>
    Hide,

    /// <summary>
    /// The window is on screen but another window is in front (Customize mode leaves it there): that window becomes the
    /// calling window, the mode is kept, and FolderSwitch comes to the front.
    /// </summary>
    Recall,

    /// <summary>The settings window shows its shortcut page, in front: the press goes to its test zone.</summary>
    TestZone,
}

/// <summary>
/// Decides what a press of the shortcut does from what is on screen. Pure: the composition reads the facts, the
/// window controller and the shortcut editor act. The notification icon and a second launch do not come here: they
/// only ever open the window.
/// </summary>
internal static class ShortcutRouting
{
    /// <param name="windowShown">The main window is on screen.</param>
    /// <param name="ownWindowInFront">The foreground window is one of FolderSwitch's (the main window, one of its menus).</param>
    /// <param name="hotkeyTestShown">The settings window shows its shortcut page, with its test zone.</param>
    /// <param name="promptOpen">One of FolderSwitch's own boxes or pickers is open.</param>
    /// <param name="boxOpen">
    /// One of FolderSwitch's own boxes is open (then <paramref name="promptOpen"/> too), with the main window shown or
    /// not: the update offer opens with the window hidden.
    /// </param>
    public static ShortcutAction Decide(bool windowShown, bool ownWindowInFront, bool hotkeyTestShown, bool promptOpen,
        bool boxOpen = false)
    {
        // The test zone is for a press made while looking at it: a window left behind another one (the settings stay
        // open when the focus goes elsewhere) is recalled instead.
        if (windowShown && hotkeyTestShown && ownWindowInFront)
        {
            return ShortcutAction.TestZone;
        }

        // Modal, a box disables the main window: showing, hiding or recalling it would leave the box unanswered.
        if (boxOpen)
        {
            return ShortcutAction.FrontBox;
        }

        if (!windowShown)
        {
            return ShortcutAction.Show;
        }

        if (promptOpen)
        {
            return ShortcutAction.None;
        }

        return ownWindowInFront ? ShortcutAction.Hide : ShortcutAction.Recall;
    }
}
