using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class FaviconLinksTests
{
    private static readonly Uri Page = new("https://a.fr/x/y");

    private static string[] Find(string html, Uri? page = null) =>
        FaviconLinks.Find(html, page ?? Page).Select(u => u.AbsoluteUri).ToArray();

    [Fact]
    public void Resolves_the_link_against_the_page_then_ends_with_the_root_favicon()
    {
        var links = Find("""<html><head><link rel="icon" href="/fav.png"></head></html>""");

        Assert.Equal(["https://a.fr/fav.png", "https://a.fr/favicon.ico"], links);
    }

    [Theory]
    [InlineData("""<link rel="SHORTCUT ICON" href="/s.ico">""")]
    [InlineData("""<LINK REL="Shortcut Icon" HREF="/s.ico">""")]
    [InlineData("""<link rel="icon" href="/s.ico">""")]
    public void Recognizes_the_icon_word_whatever_its_case(string tag) =>
        Assert.Equal(["https://a.fr/s.ico", "https://a.fr/favicon.ico"], Find(tag));

    [Fact]
    public void Recognizes_apple_touch_icons()
    {
        var links = Find("""<link rel="apple-touch-icon" href="/t.png"><link rel="apple-touch-icon-precomposed" href="/p.png">""");

        Assert.Equal(["https://a.fr/t.png", "https://a.fr/p.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Ignores_links_that_are_not_icons()
    {
        var links = Find("""<link rel="stylesheet" href="/s.css"><link rel="preload" href="/f.woff"><link rel="iconic" href="/i.png">""");

        Assert.Equal(["https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Resolves_a_relative_link_against_the_base_element()
    {
        var links = Find("""<head><base href="https://cdn.a.fr/"><link rel="icon" href="img/i.png"></head>""");

        Assert.Equal(["https://cdn.a.fr/img/i.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void A_relative_base_is_resolved_against_the_page()
    {
        var links = Find("""<base href="/assets/"><link rel="icon" href="i.png">""");

        Assert.Equal(["https://a.fr/assets/i.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Ignores_data_and_other_schemes()
    {
        var links = Find("""
            <link rel="icon" href="data:image/png;base64,AAAA">
            <link rel="icon" href="javascript:alert(1)">
            <link rel="icon" href="file:///C:/x.ico">
            <link rel="icon" href="ftp://a.fr/x.ico">
            """);

        Assert.Equal(["https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Keeps_at_most_eight_declared_icons_then_the_root_favicon()
    {
        var html = string.Concat(Enumerable.Range(1, 20).Select(i => $"""<link rel="icon" href="/{i}.png">"""));

        var links = Find(html);

        Assert.Equal(
            Enumerable.Range(1, 8).Select(i => $"https://a.fr/{i}.png").Append("https://a.fr/favicon.ico"),
            links);
    }

    [Fact]
    public void Returns_only_the_root_favicon_when_there_is_no_link()
    {
        Assert.Equal(["https://a.fr/favicon.ico"], Find("<html><body>Rien</body></html>"));
        Assert.Equal(["https://a.fr/favicon.ico"], Find(""));
    }

    [Fact]
    public void Reads_single_quoted_and_unquoted_attributes()
    {
        var links = Find("<link rel='icon' href='/q.png'><link href=/u.png rel=icon>");

        Assert.Equal(["https://a.fr/q.png", "https://a.fr/u.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Keeps_the_order_of_the_document_and_ignores_a_link_without_href()
    {
        var links = Find("""<link rel="icon"><link rel="icon" href="/1.png"><link rel="icon" href="/2.png">""");

        Assert.Equal(["https://a.fr/1.png", "https://a.fr/2.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Decodes_html_entities_of_the_href()
    {
        var links = Find("""<link rel="icon" href="/i.png?a=1&amp;b=2">""");

        Assert.Equal(["https://a.fr/i.png?a=1&b=2", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Ignores_commented_out_links()
    {
        var links = Find("""<!-- <link rel="icon" href="/old.png"> --><link rel="icon" href="/new.png">""");

        Assert.Equal(["https://a.fr/new.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void Lists_each_address_once_and_the_root_favicon_last()
    {
        var links = Find("""<link rel="icon" href="/favicon.ico"><link rel="icon" href="/b.png"><link rel="icon" href="/b.png">""");

        Assert.Equal(["https://a.fr/b.png", "https://a.fr/favicon.ico"], links);
    }

    [Fact]
    public void The_root_favicon_follows_the_page_host_and_port()
    {
        var links = Find("", new Uri("http://sub.a.fr:8080/p?q=1#f"));

        Assert.Equal(["http://sub.a.fr:8080/favicon.ico"], links);
    }
}
