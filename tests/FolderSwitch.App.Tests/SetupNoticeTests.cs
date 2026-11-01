using System.Text;
using System.Text.Json;
using FolderSwitch.App.Startup;
using FolderSwitch.App.Tests.Support;
using FolderSwitch.Core.Localization;

namespace FolderSwitch.App.Tests;

/// <summary>The notification shown after an installation or an update.</summary>
public class SetupNoticeTests
{
    [Fact]
    public void An_update_says_the_version_then_the_shortcut()
    {
        using var dir = new TempDirectory();

        var text = new SetupNotice(SetupKind.Updated, "1.3.0").Text(Load(dir), "Ctrl + ⊞ Win");

        Assert.Equal("Updated to 1.3.0\nPress Ctrl + ⊞ Win", text);
    }

    [Fact]
    public void An_installation_says_the_version_then_the_shortcut()
    {
        using var dir = new TempDirectory();

        var text = new SetupNotice(SetupKind.Installed, "1.3.0").Text(Load(dir), "Ctrl + ⊞ Win");

        Assert.Equal("Installed 1.3.0\nPress Ctrl + ⊞ Win", text);
    }

    [Fact]
    public void Without_a_registered_shortcut_only_the_version_is_said()
    {
        using var dir = new TempDirectory();

        var text = new SetupNotice(SetupKind.Updated, "1.3.0").Text(Load(dir), shortcut: null);

        Assert.Equal("Updated to 1.3.0", text);
    }

    private static Localizer Load(TempDirectory dir)
    {
        var content = new Dictionary<string, string>
        {
            [Localizer.NativeNameKey] = "English",
            ["Setup.Installed"] = "Installed {0}",
            ["Setup.Updated"] = "Updated to {0}",
            ["Setup.OpenWith"] = "Press {0}",
        };
        File.WriteAllText(dir.File("en.json"), JsonSerializer.Serialize(content), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return Localizer.LoadFromDirectory(dir.Root);
    }
}
