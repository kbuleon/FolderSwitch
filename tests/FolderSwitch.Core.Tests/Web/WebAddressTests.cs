// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class WebAddressTests
{
    [Theory]
    [InlineData("  https://www.morningstar.fr/fr/funds  ", "https://www.morningstar.fr/fr/funds")]
    [InlineData("HTTPS://Example.COM/A", "https://example.com/A")]
    [InlineData("morningstar.fr/fr/funds", "https://morningstar.fr/fr/funds")]
    [InlineData("localhost:8080", "https://localhost:8080/")]
    [InlineData("café.fr", "https://xn--caf-dma.fr/")]
    // Firefox hands out percent-decoded addresses: they are encoded again.
    [InlineData("https://a.fr/a b", "https://a.fr/a%20b")]
    [InlineData("FTP://Example.COM/pub", "ftp://example.com/pub")]
    [InlineData("ftp://café.fr/", "ftp://xn--caf-dma.fr/")]
    [InlineData("file:///C:/sessions_claude_code/bob-assistant/publish/wiki/index.html", "file:///C:/sessions_claude_code/bob-assistant/publish/wiki/index.html")]
    [InlineData("FILE:///C:/My Dir/a b.html", "file:///C:/My%20Dir/a%20b.html")]
    [InlineData(@"file:///C:\dir\index.html", "file:///C:/dir/index.html")]
    [InlineData("file://Server/Share/index.html", "file://server/Share/index.html")]
    // localhost is the local machine: the same address as without a host; the query and fragment stay.
    [InlineData("file://localhost/C:/a.html", "file:///C:/a.html")]
    [InlineData("FILE://LocalHost/C:/My Dir/a.html?x=1#top", "file:///C:/My%20Dir/a.html?x=1#top")]
    public void TryNormalize_accepts_and_normalizes(string input, string expected)
    {
        Assert.True(WebAddress.TryNormalize(input, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    [InlineData("javascript:alert(1)")] [InlineData("mailto:a@b.c")] [InlineData("https://")]
    [InlineData("gopher://example.com")] [InlineData("ftp://")] [InlineData("ftp:///x")]
    // A Windows path is a File item; a file address needs its slashes.
    [InlineData(@"C:\Users\x\index.html")] [InlineData("C:/Users/x/index.html")] [InlineData("file:C:/x")]
    // Credentials: an address with a login is never stored, and a mistyped e-mail is not a host.
    [InlineData("https://u:pw@example.com")] [InlineData("john@example.com")]
    [InlineData("ftp://user:pass@example.com/x")] [InlineData("file://u:pw@server/share/x.html")]
    public void TryNormalize_refuses(string? input) => Assert.False(WebAddress.TryNormalize(input, out _));

    [Theory]
    [InlineData("https://www.morningstar.fr/x", "morningstar.fr")]
    [InlineData("https://xn--caf-dma.fr/", "café.fr")]
    [InlineData("http://wwwexample.com/", "wwwexample.com")]
    [InlineData("ftp://www.example.com/pub", "example.com")]
    [InlineData("ftp://files.example.com/", "files.example.com")]
    public void HostLabel_drops_www(string url, string expected) => Assert.Equal(expected, WebAddress.HostLabel(url));

    [Theory]
    [InlineData("https://www.morningstar.fr/x", "morningstar.fr")]
    [InlineData("ftp://www.example.com/pub/a.zip", "example.com")]
    [InlineData("file:///C:/sessions_claude_code/bob-assistant/publish/wiki/index.html", "index.html")]
    [InlineData("file:///C:/My%20Dir/a%20b%C3%A9.html", "a bé.html")]
    [InlineData("file:///C:/dir/", "dir")]
    [InlineData("file://server/share/docs/readme.txt", "readme.txt")]
    [InlineData("file://server/", "server")]
    [InlineData("file:///C:/", "C:")]
    public void DefaultName_per_scheme(string url, string expected) => Assert.Equal(expected, WebAddress.DefaultName(url));

    [Theory]
    [InlineData("https://www.morningstar.fr/x", "morningstar.fr")]
    [InlineData("ftp://www.example.com/pub", "example.com")]
    [InlineData("file:///C:/sessions_claude_code/wiki/index.html", @"C:\sessions_claude_code\wiki\index.html")]
    [InlineData("file:///C:/My%20Dir/a%20b.html", @"C:\My Dir\a b.html")]
    [InlineData("file://server/share/index.html", @"\\server\share\index.html")]
    public void LocationLabel_per_scheme(string url, string expected) => Assert.Equal(expected, WebAddress.LocationLabel(url));

    [Theory]
    [InlineData("https://Example.com/x", "example.com")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("ftp://example.com", null)]
    [InlineData("file:///C:/x", null)]
    [InlineData("file://server/share", null)]
    [InlineData("not a url", null)]
    public void WebHost_is_only_for_web_pages(string url, string? expected) => Assert.Equal(expected, WebAddress.WebHost(url));

    [Fact]
    public void SameSite_ignores_host_case_and_trailing_slash() =>
        Assert.True(WebAddress.SameSite("https://Example.com", "https://example.com/"));

    [Theory]
    [InlineData("ftp://Example.com", "ftp://example.com/")]
    [InlineData("file:///C:/a/x.html", @"file:///C:\a\x.html")]
    // Windows paths ignore case, with or without percent-encoding.
    [InlineData("file:///C:/a.html", "file:///c:/A.html")]
    [InlineData("file:///C:/Dossier/%C3%A9t%C3%A9.html", "file:///c:/dossier/ÉTÉ.html")]
    [InlineData("file://Server/Share/X.html", "file://server/share/x.html")]
    [InlineData("file://localhost/C:/a.html", "file:///C:/a.html")]
    public void SameSite_holds_for_ftp_and_file(string a, string b) => Assert.True(WebAddress.SameSite(a, b));

    [Theory]
    [InlineData("https://example.com/a", "https://example.com/b")]
    [InlineData("https://example.com/", "http://example.com/")]
    [InlineData("https://example.com/", "not a url ://")]
    [InlineData("http://example.com/", "ftp://example.com/")]
    [InlineData("file:///C:/a/x.html", "file:///C:/a/y.html")]
    [InlineData("file://server/share/x.html", "file:///C:/share/x.html")]
    // The path of any other scheme keeps its case.
    [InlineData("https://example.com/A", "https://example.com/a")]
    [InlineData("ftp://example.com/Pub", "ftp://example.com/pub")]
    [InlineData("file:///C:/a.html", "https://c/a.html")]
    public void SameSite_tells_different_addresses_apart(string a, string b) => Assert.False(WebAddress.SameSite(a, b));

    [Theory]
    [InlineData("https://example.com/", true)]
    [InlineData("HTTP://example.com", true)]
    [InlineData("ftp://example.com", true)]
    [InlineData("FILE:///C:/x/index.html", true)]
    [InlineData("file://server/share/x.html", true)]
    [InlineData("example.com", false)]
    [InlineData("file:C:/x", false)]
    [InlineData("C:/x/index.html", false)]
    [InlineData("gopher://example.com", false)]
    [InlineData("https://", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSiteUrl_requires_a_scheme_already_present(string? url, bool expected) =>
        Assert.Equal(expected, WebAddress.IsSiteUrl(url));
}
