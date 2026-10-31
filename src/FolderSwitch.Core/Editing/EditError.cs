namespace FolderSwitch.Core.Editing;

public enum EditError
{
    None,
    NotFound,
    EmptyName,
    LastTabCannotBeRemoved,
    ColumnNotEmpty,
    CrossTabMoveNotAllowed,
}
