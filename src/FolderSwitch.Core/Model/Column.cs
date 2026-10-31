namespace FolderSwitch.Core.Model;

/// <summary>A vertical stack of <see cref="Group"/>s inside a <see cref="Tab"/>.</summary>
public sealed class Column
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Width in pixels; <c>null</c> means automatic.</summary>
    public int? Width { get; set; }

    public List<Group> Groups { get; set; } = new();
}
