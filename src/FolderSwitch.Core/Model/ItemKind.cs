namespace FolderSwitch.Core.Model;

/// <summary>What an <see cref="Item"/> points to.</summary>
public enum ItemKind
{
    Folder,
    File,

    /// <summary>A web site: the item holds an <c>http</c> or <c>https</c> address in <see cref="Item.Url"/>.</summary>
    Url,
}
