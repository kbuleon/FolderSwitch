using System.Net;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Tests.Persistence;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class FaviconsTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly TempDirectory temp = new();
    private readonly string directory;
    private readonly FaviconCache cache;
    private readonly List<string> log = [];

    public FaviconsTests()
    {
        directory = Path.Combine(temp.Root, "favicons");
        cache = new FaviconCache(directory);
    }

    public void Dispose() => temp.Dispose();

    private IFavicons Create(StubHttpHandler handler) =>
        new Favicons(cache, new FaviconFetcher(handler, "FolderSwitch/9.9.9", TimeSpan.FromSeconds(2)), log.Add);

    [Fact]
    public async Task FetchAsync_stores_the_icon_and_gives_its_path()
    {
        var handler = new StubHttpHandler()
            .OnHtml("https://a.fr/x", """<link rel="icon" href="/fav.png">""")
            .OnBytes("https://a.fr/fav.png", Png);
        var favicons = Create(handler);

        var path = await favicons.FetchAsync("https://a.fr/x", CancellationToken.None);

        Assert.Equal(Path.Combine(directory, "a.fr.png"), path);
        Assert.NotNull(path);
        Assert.Equal(Png, File.ReadAllBytes(path));
        Assert.Equal(path, favicons.CachedPath("https://a.fr/other/page"));
        Assert.Empty(log);
    }

    [Fact]
    public async Task The_icon_is_kept_per_host_whatever_the_case_and_the_form_of_the_address()
    {
        var handler = new StubHttpHandler().OnBytes("https://xn--caf-dma.fr/favicon.ico", [0x00, 0x00, 0x01, 0x00, 1]);
        var favicons = Create(handler);

        var path = await favicons.FetchAsync("café.fr", CancellationToken.None);

        Assert.Equal("xn--caf-dma.fr.ico", Path.GetFileName(path));
        Assert.Equal(path, favicons.CachedPath("HTTPS://CAFÉ.FR/a"));
    }

    [Fact]
    public async Task A_failure_gives_null_writes_nothing_and_logs_a_line_without_the_address_path()
    {
        var handler = new StubHttpHandler()
            .OnHtml("https://a.fr/secret/path?token=1", "<html></html>")
            .OnHtml("https://a.fr/favicon.ico", "<html>no</html>");
        var favicons = Create(handler);

        var path = await favicons.FetchAsync("https://a.fr/secret/path?token=1", CancellationToken.None);

        Assert.Null(path);
        Assert.False(Directory.Exists(directory) && Directory.GetFiles(directory).Length > 0);
        var line = Assert.Single(log);
        Assert.Contains("a.fr", line);
        Assert.DoesNotContain("token", line);
    }

    [Fact]
    public async Task A_cache_that_cannot_be_written_gives_null_and_logs()
    {
        // A file where the cache directory should be.
        File.WriteAllBytes(directory, [1]);
        var handler = new StubHttpHandler().OnBytes("https://a.fr/favicon.ico", Png);

        var path = await Create(handler).FetchAsync("https://a.fr/", CancellationToken.None);

        Assert.Null(path);
        Assert.Single(log);
    }

    [Fact]
    public async Task An_address_that_is_not_a_web_address_gives_null_without_request()
    {
        var handler = new StubHttpHandler();
        var favicons = Create(handler);

        Assert.Null(await favicons.FetchAsync("file:///C:/x", CancellationToken.None));
        Assert.Null(favicons.CachedPath("javascript:alert(1)"));
        Assert.Null(favicons.CachedPath(""));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void CachedPath_is_null_for_an_unknown_host() =>
        Assert.Null(Create(new StubHttpHandler()).CachedPath("https://unknown.example/"));

    [Fact]
    public async Task Cancellation_is_passed_on_to_the_caller()
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).FetchAsync("https://a.fr/", cts.Token));
        Assert.Empty(log);
    }
}
