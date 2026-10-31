namespace FolderSwitch.Core.Persistence;

/// <summary>How <see cref="ConfigStore.Load"/> obtained the configuration it returned.</summary>
public enum LoadOutcome
{
    /// <summary><c>config.json</c> was read as is.</summary>
    Loaded,

    /// <summary>There was no <c>config.json</c>; a new default configuration was created and saved.</summary>
    CreatedNew,

    /// <summary><c>config.json</c> was invalid; the latest valid backup replaced it.</summary>
    RestoredFromBackup,

    /// <summary><c>config.json</c> was invalid and no backup was valid; a new default configuration replaced it.</summary>
    ResetToDefault,
}
