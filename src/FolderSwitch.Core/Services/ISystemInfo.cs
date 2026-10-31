using FolderSwitch.Core.Activation;

namespace FolderSwitch.Core.Services;

/// <summary>What FolderSwitch needs to know about the system it runs on.</summary>
public interface ISystemInfo
{
    /// <summary>Windows 10 or Windows 11 (build 22000 or later).</summary>
    WindowsGeneration Generation { get; }

    /// <summary>The Windows display language of the user, as a culture name (<c>fr-FR</c>, <c>en-US</c>...).</summary>
    string UiCultureName { get; }

    /// <summary>Whether the user chose the dark mode for applications (Settings, Personalization, Colors).</summary>
    bool AppsUseDarkTheme { get; }

    /// <summary>
    /// Raised when <see cref="AppsUseDarkTheme"/> changes, and only then. The implementation documents
    /// the thread it is raised on.
    /// </summary>
    event EventHandler? ThemeChanged;
}
