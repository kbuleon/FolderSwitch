using System.Diagnostics;
using System.Net;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class FaviconFetcherTests
{
    private const string UserAgent = "FolderSwitch/9.9.9";
    private const string Page = "https://a.fr/x/y";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Ico = [0x00, 0x00, 0x01, 0x00, 1, 0, 9, 9];

    private static FaviconFetcher Fetcher(StubHttpHandler handler, TimeSpan? timeout = null) =>
        new(handler, UserAgent, timeout);

    private static byte[] Big(byte[] header, int length)
    {
        var data = new byte[length];
        header.CopyTo(data, 0);
        return data;
    }

    private static async Task<(byte[] Data, string Extension)?> Fetch(StubHttpHandler handler, string page = Page) =>
        await Fetcher(handler).FetchAsync(page, CancellationToken.None);

    [Fact]
    public async Task A_page_with_a_png_link_gives_the_png()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/fav.png">""")
            .OnBytes("https://a.fr/fav.png", Png);

        var result = await Fetch(handler);

        Assert.NotNull(result);
        Assert.Equal(".png", result.Value.Extension);
        Assert.Equal(Png, result.Value.Data);
        Assert.Equal([Page, "https://a.fr/fav.png"], handler.RequestedUrls);
    }

    [Fact]
    public async Task A_missing_page_still_tries_the_root_favicon()
    {
        var handler = new StubHttpHandler()
            .OnStatus(Page, HttpStatusCode.NotFound)
            .OnBytes("https://a.fr/favicon.ico", Ico);

        var result = await Fetch(handler);

        Assert.NotNull(result);
        Assert.Equal(".ico", result.Value.Extension);
        Assert.Equal(Ico, result.Value.Data);
    }

    [Fact]
    public async Task A_failing_page_still_tries_the_root_favicon()
    {
        var handler = new StubHttpHandler
        {
            Fallback = (request, _) => request.RequestUri!.AbsolutePath == "/favicon.ico"
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Ico) })
                : throw new HttpRequestException("connection reset"),
        };

        var result = await Fetch(handler);

        Assert.Equal(".ico", result?.Extension);
    }

    [Fact]
    public async Task A_page_cancelled_by_the_handler_still_tries_the_root_favicon()
    {
        var handler = new StubHttpHandler
        {
            Fallback = (request, _) => request.RequestUri!.AbsolutePath == "/favicon.ico"
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Ico) })
                : throw new OperationCanceledException("handler timeout"),
        };

        var result = await Fetch(handler);

        Assert.Equal(".ico", result?.Extension);
    }

    [Fact]
    public async Task Links_are_tried_in_order_and_the_first_image_wins()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/1.png"><link rel="icon" href="/2.png">""")
            .OnStatus("https://a.fr/1.png", HttpStatusCode.NotFound)
            .OnBytes("https://a.fr/2.png", Png)
            .OnBytes("https://a.fr/favicon.ico", Ico);

        var result = await Fetch(handler);

        Assert.Equal(".png", result?.Extension);
        Assert.DoesNotContain("https://a.fr/favicon.ico", handler.RequestedUrls);
    }

    [Fact]
    public async Task An_svg_link_then_a_missing_root_favicon_gives_null()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/fav.svg">""")
            .OnBytes("https://a.fr/fav.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray())
            .OnStatus("https://a.fr/favicon.ico", HttpStatusCode.NotFound);

        Assert.Null(await Fetch(handler));
    }

    [Fact]
    public async Task A_html_page_in_place_of_an_image_is_refused()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/fav.png">""")
            .OnHtml("https://a.fr/fav.png", "<!DOCTYPE html><html>Not found, kindly</html>")
            .OnHtml("https://a.fr/favicon.ico", "<html></html>");

        Assert.Null(await Fetch(handler));
    }

    [Fact]
    public async Task A_300_KiB_image_is_ignored()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/big.png">""")
            .OnBytes("https://a.fr/big.png", Big(Png, 300 * 1024))
            .OnStatus("https://a.fr/favicon.ico", HttpStatusCode.NotFound);

        Assert.Null(await Fetch(handler));
    }

    [Fact]
    public async Task An_oversized_image_of_unknown_length_is_ignored()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/big.png">""")
            .On("https://a.fr/big.png", () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(Big(Png, 300 * 1024)) })
            .OnStatus("https://a.fr/favicon.ico", HttpStatusCode.NotFound);

        Assert.Null(await Fetch(handler));
    }

    [Fact]
    public async Task An_image_of_exactly_256_KiB_is_kept()
    {
        var data = Big(Png, 256 * 1024);
        var handler = new StubHttpHandler().OnBytes("https://a.fr/favicon.ico", data);

        var result = await Fetch(handler);

        Assert.Equal(data.Length, result?.Data.Length);
    }

    [Fact]
    public async Task A_page_over_256_KiB_is_read_up_to_the_limit_and_its_early_links_are_used()
    {
        var html = """<link rel="icon" href="/fav.png">""" + new string('x', 300 * 1024);
        var handler = new StubHttpHandler()
            .OnHtml(Page, html)
            .OnBytes("https://a.fr/fav.png", Png);

        var result = await Fetch(handler);

        Assert.Equal(".png", result?.Extension);
    }

    [Fact]
    public async Task A_response_that_never_arrives_gives_null_within_the_timeout()
    {
        var handler = new StubHttpHandler
        {
            Fallback = async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
        };
        var clock = Stopwatch.StartNew();

        var result = await Fetcher(handler, TimeSpan.FromMilliseconds(300)).FetchAsync(Page, CancellationToken.None);

        Assert.Null(result);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");
    }

    [Fact]
    public async Task The_timeout_covers_the_whole_fetch_not_each_request()
    {
        var handler = new StubHttpHandler
        {
            Fallback = async (_, ct) =>
            {
                await Task.Delay(200, ct);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            },
        };
        var clock = Stopwatch.StartNew();

        var result = await Fetcher(handler, TimeSpan.FromMilliseconds(300)).FetchAsync(Page, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");
    }

    [Fact]
    public void The_default_timeout_is_5_seconds() =>
        Assert.Equal(TimeSpan.FromSeconds(5), FaviconFetcher.DefaultTimeout);

    [Fact]
    public async Task Cancelling_from_the_caller_is_the_only_exception()
    {
        var handler = new StubHttpHandler
        {
            Fallback = async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fetcher(handler).FetchAsync(Page, cts.Token));
    }

    [Fact]
    public async Task A_redirect_to_a_file_address_is_not_followed()
    {
        var handler = new StubHttpHandler()
            .OnRedirect(Page, "file:///C:/Windows/win.ini")
            .OnRedirect("https://a.fr/favicon.ico", "file:///C:/Windows/win.ini");

        Assert.Null(await Fetch(handler));
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Scheme == Uri.UriSchemeFile);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("ftp://a.fr/favicon.ico")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    public async Task A_redirect_to_another_scheme_is_not_followed(string location)
    {
        var handler = new StubHttpHandler()
            .OnStatus(Page, HttpStatusCode.NotFound)
            .OnRedirect("https://a.fr/favicon.ico", location);

        Assert.Null(await Fetch(handler));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Redirects_are_followed_up_to_five_times()
    {
        var handler = new StubHttpHandler()
            .OnStatus(Page, HttpStatusCode.NotFound)
            .OnRedirect("https://a.fr/favicon.ico", "https://a.fr/r1")
            .OnRedirect("https://a.fr/r1", "/r2", HttpStatusCode.MovedPermanently)
            .OnRedirect("https://a.fr/r2", "http://b.fr/r3", HttpStatusCode.TemporaryRedirect)
            .OnRedirect("http://b.fr/r3", "/r4", HttpStatusCode.PermanentRedirect)
            .OnRedirect("http://b.fr/r4", "https://c.fr/r5", HttpStatusCode.SeeOther)
            .OnBytes("https://c.fr/r5", Png);

        var result = await Fetch(handler);

        Assert.Equal(".png", result?.Extension);
    }

    [Fact]
    public async Task A_sixth_redirect_is_refused()
    {
        var handler = new StubHttpHandler()
            .OnStatus(Page, HttpStatusCode.NotFound)
            .OnRedirect("https://a.fr/favicon.ico", "https://a.fr/r1")
            .OnRedirect("https://a.fr/r1", "/r2")
            .OnRedirect("https://a.fr/r2", "/r3")
            .OnRedirect("https://a.fr/r3", "/r4")
            .OnRedirect("https://a.fr/r4", "/r5")
            .OnRedirect("https://a.fr/r5", "/r6")
            .OnBytes("https://a.fr/r6", Png);

        Assert.Null(await Fetch(handler));
        Assert.DoesNotContain("https://a.fr/r6", handler.RequestedUrls);
    }

    [Fact]
    public async Task A_redirect_loop_ends()
    {
        var handler = new StubHttpHandler()
            .OnRedirect(Page, Page)
            .OnRedirect("https://a.fr/favicon.ico", "https://a.fr/favicon.ico");

        Assert.Null(await Fetch(handler));
        Assert.Equal(12, handler.Requests.Count);
    }

    [Fact]
    public async Task A_redirected_page_resolves_its_links_against_its_final_address()
    {
        var handler = new StubHttpHandler()
            .OnRedirect("https://a.fr/", "https://www.a.fr/home")
            .OnHtml("https://www.a.fr/home", """<link rel="icon" href="fav.png">""")
            .OnBytes("https://www.a.fr/fav.png", Png);

        var result = await Fetch(handler, "https://a.fr/");

        Assert.Equal(".png", result?.Extension);
    }

    [Fact]
    public async Task A_redirect_without_location_is_a_failure()
    {
        var handler = new StubHttpHandler()
            .OnStatus(Page, HttpStatusCode.Found)
            .OnStatus("https://a.fr/favicon.ico", HttpStatusCode.Found);

        Assert.Null(await Fetch(handler));
    }

    [Fact]
    public async Task No_request_carries_a_cookie_and_all_carry_the_user_agent()
    {
        var handler = new StubHttpHandler()
            .OnHtml(Page, """<link rel="icon" href="/1.png"><link rel="icon" href="/2.png">""")
            .OnRedirect("https://a.fr/1.png", "/moved.png")
            .OnStatus("https://a.fr/moved.png", HttpStatusCode.NotFound)
            .OnBytes("https://a.fr/2.png", Png);

        var result = await Fetch(handler);

        Assert.NotNull(result);
        Assert.True(handler.Requests.Count >= 4);
        foreach (var request in handler.Requests)
        {
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.Equal(UserAgent, request.Headers.UserAgent.ToString());
            Assert.Equal(HttpMethod.Get, request.Method);
        }
    }

    [Theory]
    [InlineData("ftp://a.fr/")]
    [InlineData("file:///C:/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task A_page_that_is_not_a_web_address_is_not_requested(string page)
    {
        var handler = new StubHttpHandler();

        Assert.Null(await Fetch(handler, page));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_failing_handler_gives_null_without_exception()
    {
        var handler = new StubHttpHandler
        {
            Fallback = (_, _) => throw new HttpRequestException("no route to host"),
        };

        Assert.Null(await Fetch(handler));
    }

    [Fact]
    public async Task A_body_that_fails_while_read_gives_null_without_exception()
    {
        var handler = new StubHttpHandler()
            .OnStatus(Page, HttpStatusCode.NotFound)
            .On("https://a.fr/favicon.ico", () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingContent() });

        Assert.Null(await Fetch(handler));
    }

    private sealed class FailingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new IOException("connection closed"));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
