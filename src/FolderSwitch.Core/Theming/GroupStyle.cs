namespace FolderSwitch.Core.Theming;

/// <summary>How a group block is drawn; each theme uses exactly one style.</summary>
public enum GroupStyle
{
    /// <summary>Fluent: 4 px colored edge and a lightly tinted header.</summary>
    LeftAccent,

    /// <summary>Cards: header filled with the group color.</summary>
    SolidHeader,

    /// <summary>Pastel: body and header tinted with the group color.</summary>
    Tinted,

    /// <summary>Paper: 2 px colored underline under the title.</summary>
    Underline,

    /// <summary>Outline: 1.5 px colored border, no fill.</summary>
    Outline,

    /// <summary>Neon: colored border with a glow.</summary>
    Glow,

    /// <summary>Compact: 2 px colored bar on top.</summary>
    TopBar,
}
