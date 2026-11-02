// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Theming;

namespace FolderSwitch.Core.Tests.Theming;

/// <summary>Every theme crossed with both variants, for theories.</summary>
public static class ThemeTestData
{
    public static TheoryData<string, Variant> AllThemesAndVariants()
    {
        var data = new TheoryData<string, Variant>();
        foreach (var theme in ThemeCatalog.ThemeKeys)
        {
            data.Add(theme, Variant.Light);
            data.Add(theme, Variant.Dark);
        }

        return data;
    }
}
