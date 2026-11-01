using System.Text.Json;

namespace FolderSwitch.Presentation.Tests;

/// <summary>The keys the view models ask for exist in the shipped English file (the other languages are checked by Core).</summary>
public class StringKeysTests
{
    [Theory]
    [InlineData("Window.Customize")]
    [InlineData("Window.Done")]
    [InlineData("Context.Explorer")]
    [InlineData("Context.FileDialog")]
    [InlineData("Tab.New")]
    [InlineData("Group.New")]
    [InlineData("Confirm.DeleteTab")]
    [InlineData("Confirm.DeleteGroup")]
    [InlineData("Error.TargetUnavailable")]
    [InlineData("Error.ActionFailed")]
    [InlineData("Error.ColumnNotEmpty")]
    [InlineData("Error.LastTab")]
    public void Main_view_model_keys_exist_in_english(string key) => AssertEnglishHas(key);

    [Theory]
    [InlineData("Key.Ctrl")]
    [InlineData("Key.Alt")]
    [InlineData("Key.Shift")]
    [InlineData("Key.Win")]
    [InlineData("Key.Space")]
    [InlineData("Hotkey.Reset")]
    [InlineData("Hotkey.Test")]
    [InlineData("Hotkey.ApplyToTest")]
    [InlineData("Hotkey.Issue.SingleModifier")]
    [InlineData("Hotkey.Issue.NoModifier")]
    [InlineData("Hotkey.Issue.ReservedByWindows")]
    [InlineData("Hotkey.Issue.CtrlAltLikeAltGr")]
    [InlineData("Hotkey.Issue.TakenByOtherApp")]
    [InlineData("Default.SampleTab")]
    [InlineData("Default.SampleGroup")]
    [InlineData("Default.EmptyTab")]
    public void Settings_and_wizard_keys_exist_in_english(string key) => AssertEnglishHas(key);

    [Theory]
    [InlineData("Settings.Updates")]
    [InlineData("Tray.InstallUpdate")]
    [InlineData("Tray.Ready")]
    [InlineData("Tray.Installed")]
    [InlineData("Tray.Updated")]
    [InlineData("Tray.OpenWith")]
    [InlineData("Tray.UpdateDownloading")]
    [InlineData("Update.Available")]
    [InlineData("Update.Prompt")]
    [InlineData("Update.Install")]
    [InlineData("Update.Later")]
    [InlineData("Update.UpToDate")]
    [InlineData("Update.CheckFailed")]
    [InlineData("Update.InstallFailed")]
    public void Update_keys_exist_in_english(string key) => AssertEnglishHas(key);

    private static void AssertEnglishHas(string key)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(EnglishFile()));

        Assert.True(document.RootElement.TryGetProperty(key, out _), key);
    }

    private static string EnglishFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FolderSwitch.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("FolderSwitch.slnx was not found above " + AppContext.BaseDirectory);
        }

        return Path.Combine(directory.FullName, "src", "FolderSwitch.App", "Strings", "en.json");
    }
}
