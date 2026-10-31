using FolderSwitch.Core.Model;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>Records every save instead of writing anything.</summary>
internal sealed class FakeConfigPersistence : IConfigPersistence
{
    public List<AppConfig> Saved { get; } = new();

    public int SaveCount => Saved.Count;

    /// <summary>Runs at each save, before it is recorded: lets a test look at the world at that instant.</summary>
    public Action<AppConfig>? OnSave { get; set; }

    public void Save(AppConfig c)
    {
        OnSave?.Invoke(c);
        Saved.Add(c);
    }
}
