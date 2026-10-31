namespace FolderSwitch.Core.Services;

/// <summary>Moves an Open, Save As or folder-picker dialog to a folder.</summary>
public interface IFileDialogNavigator
{
    /// <returns><c>false</c> if the dialog could not be moved to <paramref name="folder"/>.</returns>
    bool Navigate(nint dialog, string folder);
}
