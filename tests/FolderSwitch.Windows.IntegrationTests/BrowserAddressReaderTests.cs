using System.Diagnostics;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Web;
using FolderSwitch.Windows.Browsers;
using FolderSwitch.Windows.IntegrationTests.Support;
using Xunit.Abstractions;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Runs real browsers: each test starts its own instance (new temporary profile, see <see cref="BrowserSession"/>) on
/// pages served by its own <see cref="LocalWebServer"/>, reads it, and kills that instance. No other browser window
/// is ever read. A browser that is not installed skips its tests.
/// </summary>
/// <remarks>
/// A read is retried, as the Add menu does: a browser that has just started may still be loading the page, and Chrome
/// and Edge only expose the page once a first request has turned their accessibility on. They only do so while the
/// window is visible: a window covered by others (say, opened behind the window the user works in) exposes an empty
/// document. So each test brings its own browser window to the foreground (<see cref="Foreground"/>) before reading;
/// <see cref="Hidden_Chrome_window_falls_back_to_the_address_bar"/> checks the other case.
/// </remarks>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class BrowserAddressReaderTests(ITestOutputHelper output) : IDisposable
{
    private const string TestTitle = "Page de test";
    private const string OtherTitle = "Autre page";

    private const uint SwpNoSize = 0x1;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>An attempt is bounded at 500 ms; the margin covers a thread switch and a busy machine.</summary>
    private static readonly TimeSpan AttemptLimit = TimeSpan.FromMilliseconds(800);

    private readonly BrowserAddressReader _sut = new();
    private readonly LocalWebServer _server = new(new Dictionary<string, string>
    {
        ["/page"] = LocalWebServer.Page(TestTitle),
        ["/other"] = LocalWebServer.Page(OtherTitle),
    });

    public void Dispose()
    {
        _sut.Dispose();
        _server.Dispose();
    }

    // ---- Brief tests ----

    [BrowserFact(BrowserKind.Firefox)]
    public void Reads_url_and_title_of_the_page_Firefox() => AssertReadsPage(BrowserKind.Firefox);

    [BrowserFact(BrowserKind.Chrome)]
    public void Reads_url_and_title_of_the_page_Chrome() => AssertReadsPage(BrowserKind.Chrome);

    [BrowserFact(BrowserKind.Edge)]
    public void Reads_url_and_title_of_the_page_Edge() => AssertReadsPage(BrowserKind.Edge);

    [BrowserFact(BrowserKind.Firefox)]
    public void Internal_page_gives_null_Firefox() => AssertInternalPageGivesNull(BrowserKind.Firefox, "about:blank");

    [BrowserFact(BrowserKind.Chrome)]
    public void Internal_page_gives_null_Chrome() => AssertInternalPageGivesNull(BrowserKind.Chrome, "chrome://version");

    [BrowserFact(BrowserKind.Edge)]
    public void Internal_page_gives_null_Edge() => AssertInternalPageGivesNull(BrowserKind.Edge, "edge://version");

    [Fact]
    public void Unknown_window_gives_null()
    {
        // A handle no window has (checked first).
        var stale = (nint)0x7FFF_FFF0;
        Assert.False(IsWindow(stale));

        foreach (var browser in Enum.GetValues<BrowserKind>())
        {
            Assert.Null(_sut.TryRead(nint.Zero, browser));
            Assert.Null(_sut.TryRead(stale, browser));
        }
    }

    // ---- Title source, threads, disposal ----

    /// <summary>
    /// With two tabs, Edge writes « … et 1 page de plus » in its window title: the title comes from the page itself, so
    /// it is the active page's title exactly, in every browser.
    /// </summary>
    [BrowserFact(BrowserKind.Firefox)]
    public void Title_comes_from_the_page_with_two_tabs_Firefox() => AssertTitleFromPage(BrowserKind.Firefox);

    [BrowserFact(BrowserKind.Chrome)]
    public void Title_comes_from_the_page_with_two_tabs_Chrome() => AssertTitleFromPage(BrowserKind.Chrome);

    [BrowserFact(BrowserKind.Edge)]
    public void Title_comes_from_the_page_with_two_tabs_Edge() => AssertTitleFromPage(BrowserKind.Edge);

    [BrowserFact(BrowserKind.Edge)]
    public void Reads_from_any_thread_and_gives_null_once_disposed()
    {
        using var session = StartVisible(BrowserKind.Edge, _server.Url("/page"));
        var expected = _server.Url("/page");
        Assert.Equal(expected, ReadUntilPage(session.Window, BrowserKind.Edge)?.Url);

        // From thread-pool threads (MTA), several at once: the reads are serialized and all succeed.
        var pages = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => _sut.TryRead(session.Window, BrowserKind.Edge)))
            .ToArray();
        Task.WaitAll(pages);
        Assert.All(pages, page => Assert.Equal(expected, page.Result?.Url));

        _sut.Dispose();
        Assert.Null(_sut.TryRead(session.Window, BrowserKind.Edge));
    }

    [BrowserFact(BrowserKind.Chrome)]
    public void Closed_window_gives_null()
    {
        nint window;
        using (var session = StartVisible(BrowserKind.Chrome, _server.Url("/page")))
        {
            window = session.Window;
            Assert.NotNull(ReadUntilPage(window, BrowserKind.Chrome));
        }

        Assert.Null(_sut.TryRead(window, BrowserKind.Chrome));
    }

    /// <summary>
    /// Out of sight (here moved off the screen, as if covered by other windows), Chrome does not turn the accessibility
    /// of its page on: no document, so its address bar is trusted alone once <see cref="BrowserAddressReader.DocumentGrace"/>
    /// has passed, completed with <c>https://</c>, and the title comes from the window title.
    /// </summary>
    [BrowserFact(BrowserKind.Chrome)]
    public void Hidden_Chrome_window_falls_back_to_the_address_bar()
    {
        using var session = BrowserSession.Start(BrowserKind.Chrome, _server.Url("/page"));
        WaitForTitle(session.Window, TestTitle);
        Assert.True(SetWindowPos(session.Window, 0, -20000, -20000, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate));

        var start = Stopwatch.GetTimestamp();
        var page = ReadUntilPage(session.Window, BrowserKind.Chrome);
        var elapsed = Stopwatch.GetElapsedTime(start);

        Assert.Equal(new BrowserPage("https://localhost:" + _server.Port + "/page", TestTitle), page);
        Assert.True(elapsed >= BrowserAddressReader.DocumentGrace, $"Trusted after {elapsed.TotalMilliseconds:F0} ms.");

        // The Add menu opened again later: a new series, which waits for the document again.
        Thread.Sleep(DocumentGraceTracker.SeriesGap + TimeSpan.FromMilliseconds(200));
        Assert.Null(TimedRead(session.Window, BrowserKind.Chrome));
    }

    // ---- Helpers ----

    /// <summary>Starts the browser and brings its window to the foreground, so that it is not covered by another one.</summary>
    private static BrowserSession StartVisible(BrowserKind browser, params string[] urls)
    {
        var session = BrowserSession.Start(browser, urls);
        try
        {
            Foreground.Bring(session.Window);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Waits until the window title shows the page's title: the page is loaded.</summary>
    private static void WaitForTitle(nint window, string title)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(ReadTimeout.TotalSeconds * Stopwatch.Frequency);
        while (!WindowText(window).StartsWith(title, StringComparison.Ordinal) && Stopwatch.GetTimestamp() < deadline)
        {
            Thread.Sleep(50);
        }
    }

    private void AssertReadsPage(BrowserKind browser)
    {
        var url = _server.Url("/page");
        using var session = StartVisible(browser, url);

        var page = ReadUntilPage(session.Window, browser);

        Assert.NotNull(page);
        Assert.Equal(url, page.Url);
        Assert.Equal(TestTitle, page.Title);
    }

    private void AssertInternalPageGivesNull(BrowserKind browser, string internalPage)
    {
        using var session = StartVisible(browser, internalPage);

        // Longer than the wait for the page to load and for Chrome's accessibility to come on, and than the time
        // after which the address bar alone is trusted.
        var deadline = Stopwatch.GetTimestamp() + (long)(3 * Stopwatch.Frequency);
        var attempts = 0;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            var page = TimedRead(session.Window, browser);
            attempts++;
            Assert.Null(page);
            Thread.Sleep(RetryDelay);
        }

        output.WriteLine($"{browser}: {attempts} attempts on {internalPage}, all null");
    }

    private void AssertTitleFromPage(BrowserKind browser)
    {
        var urls = new[] { _server.Url("/other"), _server.Url("/page") };
        using var session = StartVisible(browser, urls);

        var page = ReadUntilPage(session.Window, browser, p => urls.Contains(p.Url));

        Assert.NotNull(page);
        var expected = page.Url == urls[1] ? TestTitle : OtherTitle;
        var fromWindow = BrowserTitle.PageTitle(WindowText(session.Window), browser);
        output.WriteLine($"{browser}: title read '{page.Title}', from the window title '{fromWindow}'");
        Assert.Equal(expected, page.Title);
    }

    /// <summary>Reads until the result is a page (and, if given, one <paramref name="accept"/> takes), for at most <see cref="ReadTimeout"/>.</summary>
    private BrowserPage? ReadUntilPage(nint window, BrowserKind browser, Func<BrowserPage, bool>? accept = null)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(ReadTimeout.TotalSeconds * Stopwatch.Frequency);
        BrowserPage? page;
        do
        {
            page = TimedRead(window, browser);
            if (page is not null && (accept is null || accept(page)))
            {
                return page;
            }

            Thread.Sleep(RetryDelay);
        }
        while (Stopwatch.GetTimestamp() < deadline);

        return page;
    }

    private BrowserPage? TimedRead(nint window, BrowserKind browser)
    {
        var watch = Stopwatch.StartNew();
        var page = _sut.TryRead(window, browser);
        watch.Stop();
        output.WriteLine($"{browser}: {watch.Elapsed.TotalMilliseconds:F0} ms, {page?.ToString() ?? "null"}");
        Assert.True(watch.Elapsed < AttemptLimit, $"An attempt took {watch.Elapsed.TotalMilliseconds:F0} ms.");
        return page;
    }

    private static string WindowText(nint window)
    {
        var buffer = new char[1024];
        var length = GetWindowTextW(window, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hwnd, [Out] char[] buffer, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
}
