namespace FolderSwitch.Core.Services;

/// <summary>Starting FolderSwitch when the user signs in to Windows.</summary>
public interface IAutostart
{
    /// <summary>Whether FolderSwitch is set to start with Windows.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sets FolderSwitch to start with Windows by running <paramref name="exePath"/>
    /// (<paramref name="enabled"/> <c>true</c>), or not to (<c>false</c>: <paramref name="exePath"/> is
    /// not used).
    /// </summary>
    void Set(bool enabled, string exePath);
}
