using FolderSwitch.Core.Tests.Persistence;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class FaviconCacheTests : IDisposable
{
    private readonly TempDirectory temp = new();
    private readonly string directory;
    private readonly FaviconCache cache;

    public FaviconCacheTests()
    {
        directory = Path.Combine(temp.Root, "favicons");
        cache = new FaviconCache(directory);
    }

    public void Dispose() => temp.Dispose();

    private string[] Files() => Directory.GetFiles(directory).Select(f => Path.GetFileName(f)!).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void Store_then_PathFor_gives_the_file()
    {
        var path = cache.Store("example.com", [1, 2, 3], ".png");

        Assert.Equal(Path.Combine(directory, "example.com.png"), path);
        Assert.Equal(path, cache.PathFor("example.com"));
        Assert.Equal<byte>([1, 2, 3], File.ReadAllBytes(path));
    }

    [Fact]
    public void An_international_host_is_stored_in_punycode()
    {
        var path = cache.Store("café.fr", [1], ".png");

        Assert.Equal("xn--caf-dma.fr.png", Path.GetFileName(path));
        Assert.Equal(path, cache.PathFor("café.fr"));
        Assert.Equal(path, cache.PathFor("xn--caf-dma.fr"));
    }

    [Fact]
    public void The_file_name_is_lower_case()
    {
        var path = cache.Store("Example.COM", [1], ".ico");

        Assert.Equal("example.com.ico", Path.GetFileName(path));
        Assert.Equal(path, cache.PathFor("EXAMPLE.com"));
    }

    [Fact]
    public void PathFor_an_unknown_host_is_null_even_when_the_directory_is_missing()
    {
        Assert.Null(cache.PathFor("unknown.example"));

        cache.Store("example.com", [1], ".png");

        Assert.Null(cache.PathFor("unknown.example"));
        Assert.Null(cache.PathFor("com"));
    }

    [Fact]
    public void PathFor_finds_the_file_whatever_its_extension()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "example.com.gif");
        File.WriteAllBytes(file, [1]);

        Assert.Equal(file, cache.PathFor("example.com"));
    }

    [Fact]
    public void A_host_is_not_confused_with_the_host_it_is_a_prefix_of()
    {
        cache.Store("example.com.fr", [1], ".png");

        Assert.Null(cache.PathFor("example.com"));
    }

    [Fact]
    public void Store_replaces_the_previous_icon_of_the_host_even_with_another_extension()
    {
        cache.Store("example.com", [1], ".png");

        var path = cache.Store("example.com", [2], ".ico");

        Assert.Equal("example.com.ico", Path.GetFileName(path));
        Assert.Equal(path, cache.PathFor("example.com"));
        Assert.Equal(["example.com.ico"], Files());
    }

    [Fact]
    public void Store_leaves_no_temporary_file()
    {
        cache.Store("example.com", [1, 2], ".png");
        cache.Store("example.com", [3, 4], ".png");

        Assert.Equal(["example.com.png"], Files());
    }

    [Fact]
    public void A_stray_temporary_file_is_not_an_icon()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "example.com.0123.tmp"), [1]);

        Assert.Null(cache.PathFor("example.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(@"..\x")]
    public void Refuses_a_host_that_is_not_a_file_name(string host) =>
        Assert.Throws<ArgumentException>(() => cache.Store(host, [1], ".png"));

    [Theory]
    [InlineData("png")]
    [InlineData(".")]
    [InlineData(@".\x")]
    [InlineData("")]
    public void Refuses_a_malformed_extension(string extension) =>
        Assert.Throws<ArgumentException>(() => cache.Store("example.com", [1], extension));

    [Fact]
    public void An_ipv6_host_gets_a_valid_file_name()
    {
        var path = cache.Store("[::1]", [1], ".png");

        Assert.True(File.Exists(path));
        Assert.Equal(path, cache.PathFor("[::1]"));
    }
}
