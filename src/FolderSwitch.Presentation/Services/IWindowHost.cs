namespace FolderSwitch.Presentation.Services;

/// <summary>The resident main window, as seen from the view models.</summary>
public interface IWindowHost
{
    /// <summary>Hides the main window (it is never closed). Hiding a hidden window does nothing.</summary>
    void HideWindow();
}
