namespace FolderSwitch.Core.Web;

/// <summary>The page a browser window shows, as read from the window.</summary>
/// <param name="Url">The address of the page: an absolute <c>http</c> or <c>https</c> address, normalized by <see cref="WebAddress.TryNormalize"/>.</param>
/// <param name="Title">The title of the page, <c>null</c> when none could be read.</param>
public sealed record BrowserPage(string Url, string? Title);
