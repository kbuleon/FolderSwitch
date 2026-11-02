// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Persistence;

/// <summary>The configuration returned by <see cref="ConfigStore.Load"/> and how it was obtained.</summary>
public sealed record LoadResult(AppConfig Config, LoadOutcome Outcome);
