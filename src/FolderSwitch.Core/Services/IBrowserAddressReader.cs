using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Services;

/// <summary>Reads the page a browser window shows: its address and its title.</summary>
public interface IBrowserAddressReader
{
    /// <summary>
    /// One attempt to read the page shown by <paramref name="window"/>, a window of <paramref name="browser"/>
    /// captured earlier. The attempt is bounded at 500 ms; calls are serialized, so a concurrent call queues behind the
    /// current one and may wait longer in all. Callable from any thread; never throws. Retries are the caller's.
    /// </summary>
    /// <remarks>
    /// A result depends on the earlier attempts on the same window, within a series made at a steady rhythm (the Add
    /// menu's: every 200 ms). While the page document that confirms the address bar is awaited (a browser turning its
    /// accessibility on, a page loading, a window hidden behind others), the result is <c>null</c> for about 1 s at
    /// most; then the address bar is used alone. A pause in the attempts starts a new series.
    /// </remarks>
    /// <returns>
    /// The page, or <c>null</c> when the address bar is not found, when its value is not an <c>http</c> or
    /// <c>https</c> address (an internal page, text being typed), while the page document is awaited, when the
    /// window is gone, or on any failure.
    /// </returns>
    BrowserPage? TryRead(nint window, BrowserKind browser);
}
