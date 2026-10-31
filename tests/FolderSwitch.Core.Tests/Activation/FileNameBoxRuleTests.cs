using FolderSwitch.Core.Activation;

namespace FolderSwitch.Core.Tests.Activation;

/// <summary>When a file dialog's file name box gets its text back after a folder was typed in it and validated.</summary>
public class FileNameBoxRuleTests
{
    private const string Typed = @"D:\Clients\";
    private const string Original = "rapport.txt";

    // ---- MayPutTextBack ----

    [Fact]
    public void An_OK_that_timed_out_and_shows_nothing_yet_keeps_the_folder_in_the_box()
    {
        // The dialog may still validate whatever the box holds: the folder only moves it, the original text would be
        // opened or saved.
        Assert.False(FileNameBoxRule.MayPutTextBack(DialogOk.Unanswered, DialogMove.Pending));
    }

    [Theory]
    [InlineData(DialogMove.Reached)]
    [InlineData(DialogMove.Settled)]
    public void An_OK_that_timed_out_but_was_then_visibly_handled_lets_the_text_back(DialogMove move)
    {
        Assert.True(FileNameBoxRule.MayPutTextBack(DialogOk.Unanswered, move));
    }

    [Theory]
    [InlineData(DialogMove.Reached)]
    [InlineData(DialogMove.Settled)]
    [InlineData(DialogMove.Pending)]
    public void An_OK_the_dialog_answered_lets_the_text_back(DialogMove move)
    {
        // Answered: the dialog has read the box already.
        Assert.True(FileNameBoxRule.MayPutTextBack(DialogOk.Handled, move));
    }

    [Theory]
    [InlineData(DialogMove.Pending)]
    [InlineData(DialogMove.Settled)]
    public void Without_an_OK_the_text_goes_back(DialogMove move)
    {
        Assert.True(FileNameBoxRule.MayPutTextBack(DialogOk.NotSent, move));
    }

    [Theory]
    [InlineData(DialogOk.NotSent)]
    [InlineData(DialogOk.Handled)]
    [InlineData(DialogOk.Unanswered)]
    public void A_dialog_that_closed_gets_nothing(DialogOk ok)
    {
        Assert.False(FileNameBoxRule.MayPutTextBack(ok, DialogMove.Gone));
    }

    // ---- NeedsOriginal ----

    [Theory]
    [InlineData(Typed, true)]
    [InlineData("other.txt", true)]
    [InlineData(null, true)]
    [InlineData(Original, false)]
    public void An_open_or_save_box_needs_its_original_text_whenever_it_holds_another(string? current, bool expected)
    {
        Assert.Equal(expected, FileNameBoxRule.NeedsOriginal(current, Typed, Original, isFolderNameBox: false));
    }

    [Theory]
    [InlineData(Typed, true)]
    [InlineData(null, true)]
    [InlineData("Clients", false)]
    public void The_folder_picker_box_keeps_the_name_the_picker_put_there(string? current, bool expected)
    {
        // The original text there would name a subfolder of the new folder.
        Assert.Equal(expected, FileNameBoxRule.NeedsOriginal(current, Typed, Original, isFolderNameBox: true));
    }
}
