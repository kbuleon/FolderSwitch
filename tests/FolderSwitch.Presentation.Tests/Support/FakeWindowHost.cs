using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.Tests.Support;

internal sealed class FakeWindowHost : IWindowHost
{
    public int HideCount { get; private set; }

    public void HideWindow() => HideCount++;
}
