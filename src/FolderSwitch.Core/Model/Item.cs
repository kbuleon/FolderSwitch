namespace FolderSwitch.Core.Model;

/// <summary>
/// A folder or file shortcut inside a <see cref="Group"/>.
/// Exactly one of <see cref="Path"/> and <see cref="Special"/> is set.
/// </summary>
public sealed class Item
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ItemKind Kind { get; set; }

    /// <summary>Display name chosen by the user; <c>null</c> means the default name.</summary>
    public string? Name { get; set; }

    public string? Path { get; set; }

    public SpecialFolderKey? Special { get; set; }
}
