namespace FolderSwitch.Core.Editing;

/// <summary>Outcome of a <see cref="BoardEditor"/> operation that can be refused.</summary>
public readonly record struct EditResult(EditError Error)
{
    public bool Ok => Error == EditError.None;
}
