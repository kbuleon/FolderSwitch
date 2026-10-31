namespace FolderSwitch.Core.Model;

/// <summary>Root of the persisted configuration.</summary>
public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;

    public Settings Settings { get; set; } = new();

    public Appearance Appearance { get; set; } = new();

    public List<Tab> Tabs { get; set; } = new();
}
