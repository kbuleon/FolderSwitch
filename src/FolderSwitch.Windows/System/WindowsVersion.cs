namespace FolderSwitch.Windows.SystemServices;

/// <summary>The Windows version the process runs on.</summary>
internal static class WindowsVersion
{
    /// <summary>The first Windows 11 build.</summary>
    public const int Windows11Build = 22000;

    /// <summary>
    /// Whether this is Windows 11 (build 22000 or later). Since .NET 5, <see cref="Environment.OSVersion"/>
    /// reports the real version (<c>RtlGetVersion</c>), whatever the application manifest declares.
    /// </summary>
    public static bool IsWindows11 { get; } = Environment.OSVersion.Version.Build >= Windows11Build;
}
