// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Net;
using System.Text.RegularExpressions;

namespace FolderSwitch.Core.Web;

/// <summary>
/// Finds, in the HTML of a page, the addresses where its favicon may be. The analysis is a regular expression on the
/// <c>&lt;link&gt;</c> and <c>&lt;base&gt;</c> tags: no HTML parser, and a page that is not well formed is read as far
/// as its tags go.
/// </summary>
public static partial class FaviconLinks
{
    private const string RootFavicon = "/favicon.ico";

    /// <summary>The most declared icons kept, so that a page cannot make the retrieval try hundreds of addresses.</summary>
    private const int MaxLinks = 8;

    /// <summary>
    /// The candidates, best first: the <c>href</c> of each <c>&lt;link&gt;</c> whose <c>rel</c> holds the word
    /// <c>icon</c> (<c>icon</c>, <c>shortcut icon</c>) or is an <c>apple-touch-icon</c>, in the order of the document
    /// and resolved against the <c>&lt;base href&gt;</c> when there is one, otherwise against <paramref name="page"/>;
    /// then always <c>/favicon.ico</c> at the root of the page's site. Only <c>http</c> and <c>https</c> addresses are
    /// kept (<c>data:</c>, <c>javascript:</c>, <c>file:</c> are not), and each address appears once.
    /// </summary>
    /// <param name="html">The page's HTML; may be empty or truncated.</param>
    /// <param name="page">The address the HTML was read from (after redirections); an absolute web address.</param>
    public static IReadOnlyList<Uri> Find(string html, Uri page)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(page);

        var root = new Uri(page, RootFavicon);
        var links = new List<Uri>();
        var seen = new HashSet<Uri>();

        var withoutComments = CommentRegex().Replace(html, string.Empty);
        var baseUri = FindBase(withoutComments, page);

        foreach (Match tag in LinkTagRegex().Matches(withoutComments))
        {
            if (links.Count == MaxLinks)
            {
                break;
            }

            var attributes = ReadAttributes(tag.Value);
            if (!attributes.TryGetValue("rel", out var rel)
                || !IsIconRel(rel)
                || !attributes.TryGetValue("href", out var href))
            {
                continue;
            }

            var target = WebUtility.HtmlDecode(href).Trim();
            if (target.Length > 0
                && Uri.TryCreate(baseUri, target, out var link)
                && IsWeb(link)
                && link != root
                && seen.Add(link))
            {
                links.Add(link);
            }
        }

        links.Add(root);
        return links;
    }

    private static Uri FindBase(string html, Uri page)
    {
        var tag = BaseTagRegex().Match(html);
        if (tag.Success
            && ReadAttributes(tag.Value).TryGetValue("href", out var href)
            && Uri.TryCreate(page, WebUtility.HtmlDecode(href).Trim(), out var baseUri)
            && IsWeb(baseUri))
        {
            return baseUri;
        }

        return page;
    }

    private static bool IsWeb(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsIconRel(string rel)
    {
        foreach (var token in rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("icon", StringComparison.OrdinalIgnoreCase)
                || token.Equals("apple-touch-icon", StringComparison.OrdinalIgnoreCase)
                || token.Equals("apple-touch-icon-precomposed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The attributes of a tag, by lower-case name; the first occurrence of a name wins.</summary>
    private static Dictionary<string, string> ReadAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match attribute in AttributeRegex().Matches(tag))
        {
            var value = attribute.Groups["double"].Success ? attribute.Groups["double"].Value
                : attribute.Groups["single"].Success ? attribute.Groups["single"].Value
                : attribute.Groups["bare"].Success ? attribute.Groups["bare"].Value
                : null;
            if (value is not null)
            {
                attributes.TryAdd(attribute.Groups["name"].Value, value);
            }
        }

        return attributes;
    }

    [GeneratedRegex(@"<!--.*?(?:-->|\z)", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkTagRegex();

    [GeneratedRegex(@"<base\b[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BaseTagRegex();

    // name, then optionally = and a value in double quotes, single quotes or none.
    [GeneratedRegex(
        @"(?<name>[^\s=/<>""']+)(?:\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s""'<>`]+)))?",
        RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AttributeRegex();
}
