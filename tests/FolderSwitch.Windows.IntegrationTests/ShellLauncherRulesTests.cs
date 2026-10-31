using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// The rules <see cref="ShellLauncher"/> applies around <c>ShellExecuteEx</c>, without launching
/// anything. Desktop-free; tagged like the rest of this project so that the Desktop run covers them.
/// </summary>
[Trait("Category", "Desktop")]
public sealed class ShellLauncherRulesTests
{
    private const int ErrorCancelled = 1223;
    private const int ErrorFileNotFound = 2;
    private const int ErrorAccessDenied = 5;
    private const int ErrorNoAssociation = 1155;

    [Fact]
    public void A_declined_prompt_is_not_a_failure_when_the_shell_could_ask()
    {
        Assert.True(ShellLauncher.IsDeclinedByUser(ErrorCancelled, showErrors: true));
    }

    [Fact]
    public void Cancelled_without_shell_UI_is_a_failure()
    {
        // Nobody was asked (SEE_MASK_FLAG_NO_UI): nothing was opened, and nobody chose that.
        Assert.False(ShellLauncher.IsDeclinedByUser(ErrorCancelled, showErrors: false));
    }

    [Theory]
    [InlineData(ErrorFileNotFound)]
    [InlineData(ErrorAccessDenied)]
    [InlineData(ErrorNoAssociation)]
    [InlineData(0)]
    public void Other_errors_are_failures(int error)
    {
        Assert.False(ShellLauncher.IsDeclinedByUser(error, showErrors: true));
        Assert.False(ShellLauncher.IsDeclinedByUser(error, showErrors: false));
    }

    [Theory]
    [InlineData(@"C:\Dir\File.txt", @"C:\Dir")]
    [InlineData(@"C:\Dir\Sub\run.cmd", @"C:\Dir\Sub")]
    [InlineData(@"C:\File.txt", @"C:\")]
    [InlineData(@"\\server\share\Dir\File.txt", @"\\server\share\Dir")]
    [InlineData(@"\\server\share\File.txt", @"\\server\share")]
    public void The_working_directory_is_the_folder_of_a_disk_or_UNC_file(string path, string expected)
    {
        Assert.Equal(expected, ShellLauncher.WorkingDirectory(path));
    }

    [Theory]
    [InlineData("shell:Downloads")]
    [InlineData(@"shell:Downloads\File.txt")]
    [InlineData(@"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData(@"relative\File.txt")]
    [InlineData("File.txt")]
    public void Other_forms_get_no_working_directory(string path)
    {
        Assert.Null(ShellLauncher.WorkingDirectory(path));
    }
}
