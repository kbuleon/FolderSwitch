// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Windows.Browsers;
using PageDocument = FolderSwitch.Windows.Browsers.BrowserAddressReader.PageDocument;
using Reading = FolderSwitch.Windows.Browsers.BrowserAddressReader.Reading;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// The rule that turns what an attempt read (address bar, page document, window title) into a page: pure logic, no
/// browser. The values are those measured on Firefox, Chrome and Edge.
/// </summary>
public sealed class BrowserAddressReaderResolutionTests
{
    private const string EdgeWindowTitle = "Page de test et 2 pages de plus - Profil 1 – Microsoft​ Edge";

    [Fact]
    public void Firefox_decoded_address_is_confirmed_by_the_encoded_document_url()
    {
        var reading = new Reading(
            "http://www.localhost:5000/p1?q=a b&x=1#frag",
            "http://www.localhost:5000/p1?q=a%20b&x=1#frag",
            "Page de test",
            "Page de test — Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.Equal(new("http://www.localhost:5000/p1?q=a%20b&x=1#frag", "Page de test"), page);
    }

    [Fact]
    public void Chromium_address_without_scheme_takes_the_document_url_and_its_http_scheme()
    {
        var reading = new Reading(
            "www.localhost:5000/p1?q=a%20b&x=1#frag",
            "http://www.localhost:5000/p1?q=a%20b&x=1#frag",
            "Page de test",
            "Page de test - Google Chrome");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: false);

        Assert.Equal(new("http://www.localhost:5000/p1?q=a%20b&x=1#frag", "Page de test"), page);
    }

    [Fact]
    public void Root_address_without_its_slash_matches_the_document()
    {
        var reading = new Reading("exemple.fr", "https://exemple.fr/", "Exemple", null);

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: false);

        Assert.Equal(new("https://exemple.fr/", "Exemple"), page);
    }

    [Fact]
    public void Another_document_leaves_the_address_without_scheme_waiting_then_completed_with_https()
    {
        // Text typed in the address bar, or only a side panel's document: the page's own may come next.
        var reading = new Reading("exemple.org/a", "http://localhost:5000/page", "Page de test", "Page de test - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: false));
        Assert.Equal(
            "https://exemple.org/a",
            BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true)?.Url);
    }

    [Fact]
    public void Address_typed_with_its_scheme_needs_no_wait()
    {
        var reading = new Reading("https://exemple.org/a", "http://localhost:5000/page", "Page de test", "Page de test — Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.NotNull(page);
        Assert.Equal("https://exemple.org/a", page.Url);
    }

    [Fact]
    public void Document_that_matches_the_bar_wins_over_a_side_panel_found_first()
    {
        // Edge's sidebar or a Chrome split view: several RootWebArea elements, the first one not the active page's.
        PageDocument[] documents =
        [
            new("https://edge.microsoft.com/copilot", "Copilot"),
            new("http://localhost:5000/page", "Page de test"),
        ];
        var reading = new Reading("localhost:5000/page", documents, "Page de test - Microsoft​ Edge");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: false);

        Assert.Equal(new("http://localhost:5000/page", "Page de test"), page);
        Assert.Same(documents[1], BrowserAddressReader.MatchingDocument(reading));
    }

    [Fact]
    public void Address_without_scheme_and_no_document_waits_until_it_is_trusted_alone()
    {
        // A fresh Chrome or Edge session: the document only comes at the next attempt, and it may be an http page.
        var reading = new Reading("localhost:5000/page", null, null, "Page de test - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: false));
        Assert.Equal(
            new("https://localhost:5000/page", "Page de test"),
            BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("about:blank")]
    public void Document_that_is_not_a_web_page_yet_counts_as_no_document(string documentUrl)
    {
        // Chrome or Edge still turning its accessibility on (an empty document), or still loading the page.
        var reading = new Reading("localhost:5000/page", documentUrl, "", "Page de test - Microsoft​ Edge");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: false));
        Assert.Equal(
            "https://localhost:5000/page",
            BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: true)?.Url);
    }

    [Fact]
    public void Address_with_its_scheme_and_no_title_waits_until_it_is_trusted_alone()
    {
        // Firefox while the page loads: the bar already shows the address, the window title not yet the page's title.
        var reading = new Reading("http://localhost:5000/page", null, null, "Mozilla Firefox");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false));
        Assert.Equal(
            new("http://localhost:5000/page", null),
            BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: true));
    }

    [Fact]
    public void Address_with_its_scheme_needs_no_document()
    {
        var reading = new Reading("http://localhost:5000/page", null, null, "Page de test — Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.Equal(new("http://localhost:5000/page", "Page de test"), page);
    }

    [Theory]
    [InlineData("chrome://version", "chrome://version/")]
    [InlineData("edge://version", "edge://version/")]
    [InlineData("about:blank", "about:blank")]
    [InlineData("", "about:newtab")]
    [InlineData("gopher://example.org/", "gopher://example.org/")]
    [InlineData("deux mots", "http://localhost:5000/page")]
    public void Internal_page_or_text_that_is_not_an_address_gives_null(string address, string documentUrl)
    {
        var reading = new Reading(address, documentUrl, "Titre", "Titre - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true));
    }

    [Theory]
    // Firefox and Chrome show the scheme; Chrome or Edge may show a bare path: the document confirms it.
    [InlineData("file:///C:/wiki/index.html")]
    [InlineData("C:/wiki/index.html")]
    [InlineData(@"C:\wiki\index.html")]
    public void Local_page_is_confirmed_by_its_document(string address)
    {
        var reading = new Reading(address, "file:///C:/wiki/index.html", "Wiki", "Wiki - Google Chrome");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: false);

        Assert.Equal(new("file:///C:/wiki/index.html", "Wiki"), page);
    }

    [Theory]
    // The bar may show the path in another case than the document (a drive letter typed in lower case), or with the
    // local host spelled out: Windows paths ignore case, and localhost is the local machine.
    [InlineData("c:/WIKI/index.html")]
    [InlineData("file:///c:/wiki/INDEX.html")]
    [InlineData("file://localhost/C:/wiki/index.html")]
    public void Local_page_is_confirmed_by_its_document_whatever_the_case(string address)
    {
        var reading = new Reading(address, "file:///C:/wiki/index.html", "Wiki", "Wiki - Google Chrome");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: false);

        Assert.Equal(new("file:///C:/wiki/index.html", "Wiki"), page);
    }

    [Fact]
    public void Local_page_named_with_localhost_is_stored_without_it()
    {
        var reading = new Reading("file://localhost/C:/wiki/index.html", "file://localhost/C:/wiki/index.html", "Wiki", "Wiki - Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.Equal(new("file:///C:/wiki/index.html", "Wiki"), page);
    }

    [Fact]
    public void Local_page_with_spaces_is_encoded_from_firefox_decoded_address()
    {
        var reading = new Reading("file:///C:/My Dir/a b.html", "file:///C:/My%20Dir/a%20b.html", "A B", "A B - Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.Equal(new("file:///C:/My%20Dir/a%20b.html", "A B"), page);
    }

    [Fact]
    public void Network_local_page_is_confirmed_by_its_document_from_a_bare_unc_path()
    {
        var reading = new Reading(@"\\server\share\index.html", "file://server/share/index.html", "Wiki", "Wiki - Microsoft Edge");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: false);

        Assert.Equal(new("file://server/share/index.html", "Wiki"), page);
    }

    [Fact]
    public void Local_address_with_its_scheme_is_kept_alone_once_trusted()
    {
        var reading = new Reading("file:///C:/wiki/index.html", null, null, "Wiki - Google Chrome");

        Assert.Equal(
            new("file:///C:/wiki/index.html", "Wiki"),
            BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: false));
    }

    [Fact]
    public void Bare_local_path_without_a_document_gives_null()
    {
        // Without a document nothing tells a path from text being typed: it is never completed into an address.
        var reading = new Reading("C:/wiki/index.html", null, null, "Wiki - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true));
    }

    [Fact]
    public void Bare_local_path_is_not_confirmed_by_a_web_document()
    {
        var reading = new Reading("C:/wiki/index.html", "http://localhost:5000/page", "Page", "Page - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true));
    }

    [Fact]
    public void Ftp_page_is_resolved()
    {
        var reading = new Reading("ftp://files.example.org/pub", "ftp://files.example.org/pub", "Index of /pub", "Index of /pub - Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.Equal(new("ftp://files.example.org/pub", "Index of /pub"), page);
    }

    [Fact]
    public void No_address_bar_gives_null()
    {
        var reading = new Reading(null, "http://localhost:5000/page", "Page de test", "Page de test - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true));
    }

    [Fact]
    public void Title_comes_from_the_document_before_the_window_title()
    {
        var reading = new Reading("localhost:5000/page", "http://localhost:5000/page", "Page de test", EdgeWindowTitle);

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: false);

        Assert.NotNull(page);
        Assert.Equal("Page de test", page.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Document_without_a_name_leaves_the_window_title(string? documentName)
    {
        var reading = new Reading("localhost:5000/page", "http://localhost:5000/page", documentName, EdgeWindowTitle);

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Edge, trustAddressAlone: false);

        Assert.NotNull(page);
        Assert.Equal("Page de test et 2 pages de plus", page.Title);
    }

    [Fact]
    public void Document_of_another_page_does_not_give_its_name()
    {
        // A document of another view (a side panel) that came first, while the window title is the active page's.
        var reading = new Reading("exemple.org", "http://localhost:5000/panel", "Panneau", "Exemple - Google Chrome");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true);

        Assert.NotNull(page);
        Assert.Equal("Exemple", page.Title);
        Assert.Equal("https://exemple.org/", page.Url);
    }

    [Fact]
    public void No_title_anywhere_gives_a_null_title()
    {
        var reading = new Reading("https://exemple.fr/", "https://exemple.fr/", "", "Mozilla Firefox");

        var page = BrowserAddressReader.Resolve(reading, BrowserKind.Firefox, trustAddressAlone: false);

        Assert.Equal(new("https://exemple.fr/", null), page);
    }
}
