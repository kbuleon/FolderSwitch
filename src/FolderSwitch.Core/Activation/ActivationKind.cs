namespace FolderSwitch.Core.Activation;

/// <summary>The concrete action taken when an item is activated.</summary>
public enum ActivationKind
{
    /// <summary>Navigate the calling Explorer window (its active tab on Windows 11) to the folder.</summary>
    NavigateExplorer,

    /// <summary>Open the folder in a new tab of the calling Explorer window (Windows 11).</summary>
    OpenExplorerNewTab,

    /// <summary>Open the folder in a new Explorer window.</summary>
    OpenNewExplorerWindow,

    /// <summary>Move the calling file dialog to the folder.</summary>
    NavigateFileDialog,

    /// <summary>Open the file with its associated application.</summary>
    OpenFile,

    /// <summary>Open the web address in the default browser.</summary>
    OpenUrl,
}
