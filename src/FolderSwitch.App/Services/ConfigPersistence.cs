using System.IO;
using System.Security;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.App.Services;

/// <summary>
/// Saves the configuration with <see cref="ConfigStore.Save"/>. A failure of the disk never fails the edit: it is
/// logged, and reported to the user once (<paramref name="reportFailure"/>) until a save succeeds again.
/// </summary>
/// <param name="readOnly">
/// Nothing is ever written: the configuration file could not be read at startup, and the temporary configuration
/// in use must not replace it.
/// </param>
/// <param name="reportFailure">Tells the user; must not show anything synchronously (it runs inside an edit).</param>
internal sealed class ConfigPersistence(ConfigStore store, bool readOnly, Action reportFailure) : IConfigPersistence
{
    private bool _failureReported;
    private bool _readOnlyLogged;

    public void Save(AppConfig c)
    {
        ArgumentNullException.ThrowIfNull(c);

        if (readOnly)
        {
            if (!_readOnlyLogged)
            {
                _readOnlyLogged = true;
                DebugLog.Warn("Configuration not saved: it could not be read at startup, saving is off until restart");
            }

            return;
        }

        try
        {
            store.Save(c);
            _failureReported = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            DebugLog.Error("The configuration could not be saved", e);
            if (!_failureReported)
            {
                _failureReported = true;
                reportFailure();
            }
        }
    }
}
