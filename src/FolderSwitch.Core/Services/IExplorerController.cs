namespace FolderSwitch.Core.Services;

/// <summary>Drives File Explorer windows. Paths may be disk, UNC, <c>shell:</c> or <c>::{CLSID}</c> forms.</summary>
public interface IExplorerController
{
    /// <summary>Navigates the Explorer window (its active tab on Windows 11) to <paramref name="path"/>.</summary>
    /// <returns><c>false</c> if the window could not be driven.</returns>
    bool Navigate(nint explorerWindow, string path);

    /// <summary>Opens <paramref name="path"/> in a new tab of the Explorer window (Windows 11).</summary>
    /// <returns><c>false</c> if the tab could not be opened.</returns>
    bool OpenInNewTab(nint explorerWindow, string path);

    /// <summary>Opens <paramref name="path"/> in a new Explorer window.</summary>
    void OpenNewWindow(string path);
}
