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
    public void TryNormalize_accepts_and_normalizes(string input, string expected)
    {
        Assert.True(WebAddress.TryNormalize(input, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    [InlineData("javascript:alert(1)")] [InlineData("file:///C:/x")] [InlineData("mailto:a@b.c")]
    [InlineData("ftp://example.com")] [InlineData("https://")]
    // Credentials: an address with a login is never stored, and a mistyped e-mail is not a host.
    [InlineData("https://u:pw@example.com")] [InlineData("john@example.com")]
    public void TryNormalize_refuses(string? input) => Assert.False(WebAddress.TryNormalize(input, out _));

    [Theory]
    [InlineData("https://www.morningstar.fr/x", "morningstar.fr")]
    [InlineData("https://xn--caf-dma.fr/", "café.fr")]
    [InlineData("http://wwwexample.com/", "wwwexample.com")]
    public void HostLabel_drops_www(string url, string expected) => Assert.Equal(expected, WebAddress.HostLabel(url));

    [Fact]
    public void SameSite_ignores_host_case_and_trailing_slash() =>
        Assert.True(WebAddress.SameSite("https://Example.com", "https://example.com/"));

    [Theory]
    [InlineData("https://example.com/a", "https://example.com/b")]
    [InlineData("https://example.com/", "http://example.com/")]
    [InlineData("https://example.com/", "not a url ://")]
    public void SameSite_tells_different_addresses_apart(string a, string b) => Assert.False(WebAddress.SameSite(a, b));

    [Theory]
    [InlineData("https://example.com/", true)]
    [InlineData("HTTP://example.com", true)]
    [InlineData("example.com", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("https://", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWebUrl_requires_a_scheme_already_present(string? url, bool expected) =>
        Assert.Equal(expected, WebAddress.IsWebUrl(url));
}
