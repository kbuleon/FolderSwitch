namespace FolderSwitch.Core.Services;

/// <summary>Opens files through the Windows shell.</summary>
public interface IShellLauncher
{
    /// <summary>Opens the file with its associated application.</summary>
    /// <returns>
    /// <c>true</c> when the file was opened, or when the user declined a prompt of the shell; <c>false</c> when the shell
    /// could not open it and has already told the user so, with its own message.
    /// </returns>
    bool OpenFile(string path);
}
