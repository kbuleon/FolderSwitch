using FolderSwitch.Core.Activation;

namespace FolderSwitch.Core.Web;

/// <summary>
/// The title of a web page as a browser writes it in its window title. Pure logic; only a fallback for the
/// title read from the page itself.
/// </summary>
/// <remarks>
/// A browser appends its name to the title of the page: <c>Page — Mozilla Firefox</c>, <c>Page - Google Chrome</c>,
/// <c>Page - Profil 1 – Microsoft Edge</c> (a no-break space inside « Profil 1 », an en dash, a zero-width space after
/// « Microsoft »). The private forms are translated and unknown in most languages, so the rule does not list them:
/// the window title is cut at the last separator (a dash, an en dash or an em dash, between spaces) whose following
/// text contains the name of the browser (<c>Firefox</c>, <c>Chrome</c>, <c>Edge</c>); then a leading
/// <c>[InPrivate]</c> and a terminal « Profil … » segment are removed. Edge's « … et N pages de plus » is not.
/// </remarks>
public static class BrowserTitle
{
    private const string InPrivatePrefix = "[InPrivate]";

    // « Profil 1 » (fr, de, pl), « Profile 1 », « Perfil 1 », « Profilo 1 », « Profiel 1 ».
    private static readonly string[] ProfileWords = ["Profil", "Profile", "Perfil", "Profilo", "Profiel"];

    /// <summary>
    /// The page title without the browser's suffix, trimmed; <c>null</c> when nothing remains (an empty title, or the
    /// name of the browser alone). A title with no trace of the browser's name is returned as it is.
    /// </summary>
    public static string? PageTitle(string? windowTitle, BrowserKind browser)
    {
        var title = Trim(windowTitle);
        if (title.Length == 0)
        {
            return null;
        }

        var name = NameOf(browser);
        var cut = LastSeparatorBefore(title, name);
        if (cut >= 0)
        {
            title = Trim(title[..cut]);
        }
        else if (title.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            // No page: the window carries the name of the browser alone.
            return null;
        }

        if (title.StartsWith(InPrivatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            title = Trim(title[InPrivatePrefix.Length..]);
        }

        title = StripProfile(title);
        return title.Length == 0 ? null : title;
    }

    private static string NameOf(BrowserKind browser) => browser switch
    {
        BrowserKind.Firefox => "Firefox",
        BrowserKind.Chrome => "Chrome",
        BrowserKind.Edge => "Edge",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, null),
    };

    /// <summary>
    /// The index of the last separator whose following text contains <paramref name="name"/>, or -1. The scan goes
    /// from the end, so that Chrome's profile name after its own (<c>Page - Google Chrome - Karl</c>) is cut too.
    /// </summary>
    private static int LastSeparatorBefore(string title, string name)
    {
        for (var i = title.Length - 2; i >= 1; i--)
        {
            if (IsSeparator(title, i) && title.AsSpan(i + 1).Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A dash, an en dash or an em dash with white space on both sides (a dash inside a word is not one).</summary>
    private static bool IsSeparator(string title, int index) =>
        title[index] is '-' or '–' or '—'
        && char.IsWhiteSpace(title[index - 1])
        && char.IsWhiteSpace(title[index + 1]);

    /// <summary>Removes a terminal « Profil … » segment, when something comes before its separator.</summary>
    private static string StripProfile(string title)
    {
        for (var i = title.Length - 2; i >= 1; i--)
        {
            if (!IsSeparator(title, i))
            {
                continue;
            }

            var head = Trim(title[..i]);
            return head.Length > 0 && IsProfile(Trim(title[(i + 1)..])) ? head : title;
        }

        return title;
    }

    /// <summary>A profile word followed by white space and more (« Profil 1 »).</summary>
    private static bool IsProfile(string segment)
    {
        foreach (var word in ProfileWords)
        {
            if (segment.Length > word.Length
                && segment.StartsWith(word, StringComparison.OrdinalIgnoreCase)
                && char.IsWhiteSpace(segment[word.Length]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Trims white space (a no-break space included) and the zero-width space Edge writes in its name.</summary>
    private static string Trim(string? text) => text is null ? string.Empty : text.Trim().Trim('\u200B').Trim();
}
