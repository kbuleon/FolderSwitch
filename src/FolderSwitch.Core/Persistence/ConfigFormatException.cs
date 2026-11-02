// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Persistence;

/// <summary>The configuration text is not a usable FolderSwitch configuration.</summary>
public sealed class ConfigFormatException : Exception
{
    public ConfigFormatException(string message)
        : base(message)
    {
    }

    public ConfigFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
