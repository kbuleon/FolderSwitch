// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.App.Themes;

/// <summary>
/// The names of the resources <see cref="ThemeApplier"/> puts in the application's resources. The views read them with
/// <c>DynamicResource</c> only, so that a change of appearance reaches them at once.
/// </summary>
internal static class ThemeKeys
{
    // ----- The theme's tokens (ThemeTokens), as brushes -----

    /// <summary>Window background.</summary>
    public const string Background = "Fs.Background";

    /// <summary>Surface of group blocks and of the title bar buttons.</summary>
    public const string Card = "Fs.Card";

    /// <summary>Text on <see cref="Card"/>.</summary>
    public const string Text = "Fs.Text";

    /// <summary>Secondary text on <see cref="Card"/>.</summary>
    public const string Muted = "Fs.Muted";

    /// <summary>Hairlines (may be translucent).</summary>
    public const string Border = "Fs.Border";

    /// <summary>Text drawn on the window background: title, tabs.</summary>
    public const string OnBackgroundText = "Fs.OnBackgroundText";

    /// <summary>Secondary text drawn on the window background: title, inactive tabs.</summary>
    public const string OnBackgroundMuted = "Fs.OnBackgroundMuted";

    // ----- Metrics and flags -----

    /// <summary>Corner radius of group blocks (<c>CornerRadius</c>).</summary>
    public const string GroupRadius = "Fs.GroupRadius";

    /// <summary>Corner radius of an item's hover highlight: the group's, at most 7 (<c>CornerRadius</c>).</summary>
    public const string ItemRadius = "Fs.ItemRadius";

    /// <summary>Tighter rows (<c>bool</c>).</summary>
    public const string Dense = "Fs.Dense";

    /// <summary>Group titles in capitals (<c>bool</c>).</summary>
    public const string UppercaseTitles = "Fs.UppercaseTitles";

    /// <summary>Space between columns (<c>double</c>).</summary>
    public const string ColumnGap = "Fs.ColumnGap";

    /// <summary>Space between the groups of a column (<c>double</c>).</summary>
    public const string GroupGap = "Fs.GroupGap";

    /// <summary>
    /// Margin of the board in normal use (<c>Thickness</c>): <see cref="GroupGap"/> between the tab bar's line and the
    /// groups, as between two groups.
    /// </summary>
    public const string BoardMargin = "Fs.BoardMargin";

    /// <summary>Padding of an item row (<c>Thickness</c>).</summary>
    public const string ItemPadding = "Fs.ItemPadding";

    /// <summary>Font size of an item's name (<c>double</c>).</summary>
    public const string ItemFontSize = "Fs.ItemFontSize";

    /// <summary>Everything at once (<see cref="AppearanceState"/>): for the views that compute colors of their own.</summary>
    public const string Appearance = "Fs.Appearance";

    // ----- Derived colors -----

    /// <summary>The 1 px window border where the system does not round the corners: opaque.</summary>
    public const string WindowBorder = "Fs.WindowBorder";

    /// <summary>Hover highlight of a control drawn on the window background (a tab).</summary>
    public const string Hover = "Fs.Hover";

    /// <summary>Border of a title bar button under the mouse.</summary>
    public const string ButtonHoverBorder = "Fs.ButtonHoverBorder";

    /// <summary>The accent of Customize mode (the « Done » button).</summary>
    public const string Accent = "Fs.Accent";

    /// <summary>Text on <see cref="Accent"/>.</summary>
    public const string OnAccent = "Fs.OnAccent";

    /// <summary>Background of the close cross under the mouse.</summary>
    public const string CloseHover = "Fs.CloseHover";

    /// <summary>The close cross on <see cref="CloseHover"/>.</summary>
    public const string OnCloseHover = "Fs.OnCloseHover";

    /// <summary>Dotted background of the board in Customize mode.</summary>
    public const string BoardDots = "Fs.BoardDots";

    /// <summary>Dashed outline of a column in Customize mode.</summary>
    public const string ColumnOutline = "Fs.ColumnOutline";

    // ----- Customize mode -----

    /// <summary>Corner radius of group blocks as a number (<c>double</c>), for shapes (« + Group »).</summary>
    public const string GroupRadiusValue = "Fs.GroupRadiusValue";

    /// <summary>Dashed outline of the « + » buttons drawn on the window background.</summary>
    public const string AddOutline = "Fs.AddOutline";

    /// <summary>Fill of the « + » buttons drawn on the window background: the background, slightly see-through.</summary>
    public const string AddFill = "Fs.AddFill";

    /// <summary>The bar of a column's resize grip, at rest.</summary>
    public const string ResizeGrip = "Fs.ResizeGrip";

    /// <summary>Ring of a color swatch: a neutral grey that shows on any color.</summary>
    public const string SwatchRing = "Fs.SwatchRing";

    // ----- Menus, panels and dialogs -----

    /// <summary>Surface of the menus, the settings and About windows and the dialogs.</summary>
    public const string Menu = "Fs.Menu";

    /// <summary>Text on <see cref="Menu"/> (4.5:1 at least).</summary>
    public const string MenuText = "Fs.MenuText";

    /// <summary>Secondary text on <see cref="Menu"/> (3:1 at least).</summary>
    public const string MenuMuted = "Fs.MenuMuted";

    /// <summary>A menu entry under the mouse.</summary>
    public const string MenuHover = "Fs.MenuHover";

    /// <summary>Fill of the chosen tile or button of a menu.</summary>
    public const string MenuSelected = "Fs.MenuSelected";

    /// <summary>Border of the chosen tile or button of a menu.</summary>
    public const string MenuSelectedBorder = "Fs.MenuSelectedBorder";

    /// <summary>The track of a segmented control.</summary>
    public const string SegmentTrack = "Fs.SegmentTrack";

    /// <summary>Fill of a key cap (the shortcut editor).</summary>
    public const string KeyCap = "Fs.KeyCap";

    /// <summary>Border of a key cap.</summary>
    public const string KeyCapBorder = "Fs.KeyCapBorder";

    /// <summary>The shadow of a menu or a dialog (<c>Effect</c>).</summary>
    public const string FlyoutShadow = "Fs.FlyoutShadow";

    /// <summary>A refusal (text on <see cref="DangerBackground"/>, 4.5:1).</summary>
    public const string Danger = "Fs.Danger";

    public const string DangerBackground = "Fs.DangerBackground";

    /// <summary>A warning (text on <see cref="WarningBackground"/>, 4.5:1).</summary>
    public const string Warning = "Fs.Warning";

    public const string WarningBackground = "Fs.WarningBackground";

    /// <summary>A success (text on <see cref="SuccessBackground"/>, 4.5:1).</summary>
    public const string Success = "Fs.Success";

    public const string SuccessBackground = "Fs.SuccessBackground";

    /// <summary>A piece of advice, neither good nor bad news (text on <see cref="InfoBackground"/>, 4.5:1).</summary>
    public const string Info = "Fs.Info";

    public const string InfoBackground = "Fs.InfoBackground";
}
