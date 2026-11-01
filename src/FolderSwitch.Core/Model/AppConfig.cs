namespace FolderSwitch.Core.Model;

/// <summary>Root of the persisted configuration.</summary>
public sealed class AppConfig
{
    /// <summary>Format version of the file; kept equal to <c>ConfigSerializer.CurrentSchemaVersion</c>.</summary>
    public int SchemaVersion { get; set; } = 2;

    public Settings Settings { get; set; } = new();

    public Appearance Appearance { get; set; } = new();

    public List<Tab> Tabs { get; set; } = new();
}
