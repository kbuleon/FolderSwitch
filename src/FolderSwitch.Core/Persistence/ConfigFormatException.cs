namespace FolderSwitch.Core.Persistence;

/// <summary>The configuration text is not a usable FolderSwitch configuration.</summary>
public sealed class ConfigFormatException : Exception
{
    public ConfigFormatException(string message)
        : base(message)
    {
    }

    public ConfigFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
