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
    [InlineData("file:///C:/Users/", "file:///C:/Users/")]
    [InlineData("deux mots", "http://localhost:5000/page")]
    public void Internal_page_or_text_that_is_not_an_address_gives_null(string address, string documentUrl)
    {
        var reading = new Reading(address, documentUrl, "Titre", "Titre - Google Chrome");

        Assert.Null(BrowserAddressReader.Resolve(reading, BrowserKind.Chrome, trustAddressAlone: true));
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
