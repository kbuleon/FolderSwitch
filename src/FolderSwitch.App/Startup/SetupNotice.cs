using FolderSwitch.Core.Localization;

namespace FolderSwitch.App.Startup;

/// <summary>What Velopack just did before this start.</summary>
internal enum SetupKind
{
    /// <summary>Installed by the installer, over an existing configuration or not (Velopack's first run).</summary>
    Installed,

    /// <summary>Updated by FolderSwitch itself, then restarted by Velopack.</summary>
    Updated,
}

/// <summary>
/// An installation or an update that just ended, at version <paramref name="Version"/>: once FolderSwitch is ready, a
/// notification says so, since the installer and the updater end without a word.
/// </summary>
internal sealed record SetupNotice(SetupKind Kind, string Version)
{
    /// <summary>
    /// « FolderSwitch 1.3.0 has been installed » or « FolderSwitch has been updated to version 1.3.0 », then, on a second
    /// line when the shortcut is registered (<paramref name="shortcut"/>, as the user reads it), « Press Ctrl + ⊞ Win to
    /// open FolderSwitch ».
    /// </summary>
    public string Text(Localizer localizer, string? shortcut)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var text = localizer.Format(Kind == SetupKind.Updated ? "Tray.Updated" : "Tray.Installed", Version);
        return shortcut is null ? text : text + "\n" + localizer.Format("Tray.OpenWith", shortcut);
    }
}
