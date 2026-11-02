// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.Tests.Support;

internal sealed class FakeWindowHost : IWindowHost
{
    public int HideCount { get; private set; }

    public void HideWindow() => HideCount++;
}
