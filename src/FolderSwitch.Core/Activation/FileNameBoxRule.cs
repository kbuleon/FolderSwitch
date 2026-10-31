namespace FolderSwitch.Core.Activation;

/// <summary>What became of the OK sent to a file dialog once a folder was typed in its file name box.</summary>
public enum DialogOk
{
    /// <summary>Not sent: a message before it failed, or the box did not hold the folder.</summary>
    NotSent,

    /// <summary>Sent, and the dialog answered it: it has read the box.</summary>
    Handled,

    /// <summary>Sent, but it timed out: the dialog may still read the box and validate what it holds then.</summary>
    Unanswered,
}

/// <summary>Where a file dialog stands after that OK, as its navigator saw it last.</summary>
public enum DialogMove
{
    /// <summary>It shows the folder.</summary>
    Reached,

    /// <summary>
    /// It does not, and its file name box no longer holds the folder typed: it put a text of its own back, so it is done
    /// with that OK.
    /// </summary>
    Settled,

    /// <summary>It does not, and nothing shows that it is done with that OK (or no OK was sent).</summary>
    Pending,

    /// <summary>The dialog has closed.</summary>
    Gone,
}

/// <summary>
/// When a file dialog's file name box gets its text back after a folder was typed in it and validated, to move the
/// dialog. Pure decisions; the file-dialog navigator gathers the facts and acts.
/// </summary>
/// <remarks>
/// A dialog validates whatever its box holds when it handles the OK. One that has not visibly finished with an OK that
/// timed out may still handle it, after any text sent meanwhile (sent messages come before a posted one, should it defer
/// its OK): the original text put back then would be validated, as the file to open or save, or a subfolder to pick.
/// So that text is left as it is, the folder typed, which a late OK only browses to.
/// </remarks>
public static class FileNameBoxRule
{
    /// <summary>
    /// Whether the box may be given a text back at all: not when an OK that timed out shows no outcome yet
    /// (<see cref="DialogOk.Unanswered"/> and <see cref="DialogMove.Pending"/>), nor when the dialog has closed.
    /// </summary>
    public static bool MayPutTextBack(DialogOk lastOk, DialogMove move) =>
        move != DialogMove.Gone && !(lastOk == DialogOk.Unanswered && move == DialogMove.Pending);

    /// <summary>
    /// Whether the box needs its original text, given what it holds now (<paramref name="current"/>, <c>null</c> when it
    /// could not be read): an Open / Save As box whenever it holds anything else; the folder picker's box only while it
    /// still holds the folder typed (or is unknown), since the picker puts the new folder's name there itself and the
    /// original text would name a subfolder of the new folder.
    /// </summary>
    public static bool NeedsOriginal(string? current, string typed, string original, bool isFolderNameBox) =>
        isFolderNameBox ? current is null || current == typed : current != original;
}
