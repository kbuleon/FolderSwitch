using FolderSwitch.Core.Services;
using Microsoft.Win32;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// Starting FolderSwitch with Windows: a value of the current user's <c>Run</c> key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>) that holds the quoted path of the
/// executable. No administrator rights needed.
/// </summary>
/// <remarks>
/// <see cref="IsEnabled"/> only says that the value exists: it does not check the path it holds, nor
/// whether the user disabled the entry in Task Manager (recorded by Windows elsewhere, in
/// <c>Explorer\StartupApproved\Run</c>, which is not touched here).
/// </remarks>
/// <param name="runKeyPath">The key, under <c>HKEY_CURRENT_USER</c>, that holds the value (another key for tests).</param>
/// <param name="valueName">The name of the value.</param>
public sealed class Autostart(
    string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string valueName = "FolderSwitch") : IAutostart
{
    /// <inheritdoc/>
    /// <remarks><c>false</c> too when the key cannot be read.</remarks>
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
                return key?.GetValue(valueName) is not null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Enabling writes <c>"&lt;exePath&gt;"</c> (quoted once, even if <paramref name="exePath"/> already
    /// is), replacing any previous value; disabling deletes the value if there is one.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="exePath"/> is blank while enabling.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be written.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be written.</exception>
    /// <exception cref="IOException">The registry failed.</exception>
    public void Set(bool enabled, string exePath)
    {
        if (enabled)
        {
            var path = exePath?.Trim().Trim('"') ?? string.Empty;
            ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(exePath));

            using var key = Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true);
            key.SetValue(valueName, "\"" + path + "\"", RegistryValueKind.String);
            return;
        }

        using var existing = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        existing?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
