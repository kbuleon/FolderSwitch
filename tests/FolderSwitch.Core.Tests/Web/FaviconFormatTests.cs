// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Tests.Web;

public class FaviconFormatTests
{
    private static byte[] Padded(params byte[] header) => [.. header, .. new byte[32]];

    [Fact]
    public void Recognizes_ico() => Assert.Equal(".ico", FaviconFormat.ExtensionOf(Padded(0x00, 0x00, 0x01, 0x00)));

    [Fact]
    public void Recognizes_png() =>
        Assert.Equal(".png", FaviconFormat.ExtensionOf(Padded(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)));

    [Theory]
    [InlineData("GIF87a")]
    [InlineData("GIF89a")]
    public void Recognizes_gif(string signature) =>
        Assert.Equal(".gif", FaviconFormat.ExtensionOf(Padded(Encoding.ASCII.GetBytes(signature))));

    [Fact]
    public void Recognizes_jpeg() => Assert.Equal(".jpg", FaviconFormat.ExtensionOf(Padded(0xFF, 0xD8, 0xFF, 0xE0)));

    [Fact]
    public void Recognizes_bmp() => Assert.Equal(".bmp", FaviconFormat.ExtensionOf(Padded(0x42, 0x4D, 0x36, 0x00, 0x00)));

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("<?xml version=\"1.0\"?><svg></svg>")]
    [InlineData("<!DOCTYPE html><html></html>")]
    [InlineData("<html></html>")]
    [InlineData("BMW is a text, not a bitmap")]
    public void Refuses_what_is_not_a_raster_image(string text) =>
        Assert.Null(FaviconFormat.ExtensionOf(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Refuses_empty_and_truncated_data()
    {
        Assert.Null(FaviconFormat.ExtensionOf([]));
        Assert.Null(FaviconFormat.ExtensionOf([0x00, 0x00, 0x01]));
        Assert.Null(FaviconFormat.ExtensionOf([0x89, 0x50, 0x4E, 0x47]));
    }
}
