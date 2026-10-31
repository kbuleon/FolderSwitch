using FolderSwitch.Core.Model;

namespace FolderSwitch.Presentation.Services;

/// <summary>Writes the configuration; called by the view models after every change.</summary>
public interface IConfigPersistence
{
    /// <summary>
    /// Writes <paramref name="c"/> at once. Called on the UI thread; the implementation deals with its own
    /// I/O failures, so that an edit never fails because the disk did.
    /// </summary>
    void Save(AppConfig c);
}
