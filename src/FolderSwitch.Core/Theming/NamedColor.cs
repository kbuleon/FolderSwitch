namespace FolderSwitch.Core.Theming;

/// <summary>A color of a catalog list: a stable key stored in the configuration and its <c>#rrggbb</c> value.</summary>
public sealed record NamedColor(string Key, string Hex);
