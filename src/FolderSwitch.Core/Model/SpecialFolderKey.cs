namespace FolderSwitch.Core.Model;

/// <summary>Well-known Windows locations that have no fixed disk path (or whose path varies per user).</summary>
public enum SpecialFolderKey
{
    Desktop,
    Documents,
    Downloads,
    Pictures,
    Music,
    Videos,
    OneDrive,
    UserProfile,
    ThisPC,
    RecycleBin,
    ProgramFiles,
    RoamingAppData,
    LocalAppData,
    Temp,
    Startup,
    Windows,
}
