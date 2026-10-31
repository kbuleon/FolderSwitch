using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>Records the autostart changes; <see cref="Throws"/> makes them fail like a refused registry write.</summary>
internal sealed class FakeAutostart : IAutostart
{
    public bool IsEnabled { get; set; }

    public Exception? Throws { get; set; }

    public List<(bool Enabled, string ExePath)> Calls { get; } = new();

    public void Set(bool enabled, string exePath)
    {
        Calls.Add((enabled, exePath));
        if (Throws is { } exception)
        {
            throw exception;
        }

        IsEnabled = enabled;
    }
}
