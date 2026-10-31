namespace FolderSwitch.Core.Model;

/// <summary>A named board made of <see cref="Column"/>s.</summary>
public sealed class Tab
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<Column> Columns { get; set; } = new();
}
