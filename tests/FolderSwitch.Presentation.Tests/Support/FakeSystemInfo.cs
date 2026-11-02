// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>A system whose display language and application theme the test decides.</summary>
internal sealed class FakeSystemInfo : ISystemInfo
{
    private readonly List<EventHandler> _themeChanged = new();

    public event EventHandler? ThemeChanged
    {
        add
        {
            if (value is not null)
            {
                _themeChanged.Add(value);
            }
        }
        remove
        {
            if (value is not null)
            {
                _themeChanged.Remove(value);
            }
        }
    }

    public WindowsGeneration Generation { get; set; } = WindowsGeneration.Windows11;

    public string UiCultureName { get; set; } = "en-US";

    public bool AppsUseDarkTheme { get; private set; }

    public int ThemeChangedSubscribers => _themeChanged.Count;

    /// <summary>The user switches the Windows application mode: raises <see cref="ThemeChanged"/> when it changes.</summary>
    public void SetDark(bool dark)
    {
        if (dark == AppsUseDarkTheme)
        {
            return;
        }

        AppsUseDarkTheme = dark;
        foreach (var handler in _themeChanged.ToList())
        {
            handler(this, EventArgs.Empty);
        }
    }
}
