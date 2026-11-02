// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class BrowserTitleTests
{
    [Theory]
    [InlineData("Morningstar France — Mozilla Firefox", BrowserKind.Firefox, "Morningstar France")]
    [InlineData("Morningstar France — Navigation privée de Mozilla Firefox", BrowserKind.Firefox, "Morningstar France")]
    [InlineData("Morningstar - Google Chrome", BrowserKind.Chrome, "Morningstar")]
    [InlineData("Morningstar - Google Chrome (Navigation privée)", BrowserKind.Chrome, "Morningstar")]
    [InlineData("Morningstar - Profil 1 - Microsoft\u200b Edge", BrowserKind.Edge, "Morningstar")]
    [InlineData("[InPrivate] Morningstar - Microsoft\u200b Edge", BrowserKind.Edge, "Morningstar")]
    [InlineData("Mozilla Firefox", BrowserKind.Firefox, null)]
    [InlineData("", BrowserKind.Chrome, null)]
    public void PageTitle_strips_the_browser(string title, BrowserKind browser, string? expected) =>
        Assert.Equal(expected, BrowserTitle.PageTitle(title, browser));

    // Edge as it writes its titles: a no-break space inside « Profil 1 » and around the separator, an en dash,
    // a zero-width space after « Microsoft ».
    [Theory]
    [InlineData("Morningstar\u00a0-\u00a0Profil\u00a01\u00a0\u2013\u00a0Microsoft\u200b Edge", "Morningstar")]
    [InlineData("Morningstar - Profil\u00a01 \u2013 Microsoft\u200b Edge", "Morningstar")]
    [InlineData("Morningstar \u2013 Microsoft\u200b Edge", "Morningstar")]
    [InlineData("Morningstar\u00a0\u2013\u00a0Microsoft\u200b Edge", "Morningstar")]
    [InlineData("[InPrivate] Morningstar - Profil 1 \u2013 Microsoft\u200b Edge", "Morningstar")]
    [InlineData("Microsoft\u200b Edge", null)]
    public void PageTitle_copes_with_the_characters_of_Edge(string title, string? expected) =>
        Assert.Equal(expected, BrowserTitle.PageTitle(title, BrowserKind.Edge));

    [Theory]
    // The transient title of a window that has not loaded its page yet is a title like any other.
    [InlineData("Sans titre - Google Chrome", BrowserKind.Chrome, "Sans titre")]
    // Chrome appends the name of the profile after its own.
    [InlineData("Morningstar - Google Chrome - Karl", BrowserKind.Chrome, "Morningstar")]
    // A page title that has dashes of its own, and a word that holds a dash is not a separator.
    [InlineData("Plan A - Plan B - Mozilla Firefox", BrowserKind.Firefox, "Plan A - Plan B")]
    [InlineData("Peer-to-peer — Mozilla Firefox", BrowserKind.Firefox, "Peer-to-peer")]
    // « Edge » in the page title: only the part after the last separator is the browser.
    [InlineData("Edge cases - Microsoft\u200b Edge", BrowserKind.Edge, "Edge cases")]
    // A page called « Profil 1 » alone is a page title, not a profile name.
    [InlineData("Profil 1 - Microsoft\u200b Edge", BrowserKind.Edge, "Profil 1")]
    // Case of the browser name.
    [InlineData("Morningstar - GOOGLE CHROME", BrowserKind.Chrome, "Morningstar")]
    // Only the name of the browser: nothing left.
    [InlineData("Google Chrome", BrowserKind.Chrome, null)]
    [InlineData("  Google Chrome  ", BrowserKind.Chrome, null)]
    [InlineData(" - Google Chrome", BrowserKind.Chrome, null)]
    [InlineData("[InPrivate] - Microsoft\u200b Edge", BrowserKind.Edge, null)]
    [InlineData("   ", BrowserKind.Firefox, null)]
    [InlineData(null, BrowserKind.Firefox, null)]
    // A title that carries no browser name at all is kept as it is (trimmed).
    [InlineData("  Morningstar  ", BrowserKind.Chrome, "Morningstar")]
    public void PageTitle_edge_cases(string? title, BrowserKind browser, string? expected) =>
        Assert.Equal(expected, BrowserTitle.PageTitle(title, browser));
}
