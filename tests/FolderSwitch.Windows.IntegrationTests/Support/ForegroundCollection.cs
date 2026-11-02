// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// Tests that open windows and move the foreground: run alone, never in parallel with another
/// collection, so that they do not take the foreground from one another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ForegroundCollection
{
    public const string Name = "Foreground";
}
