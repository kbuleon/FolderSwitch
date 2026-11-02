// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>Recognises the virtual-location forms that have no disk path: <c>shell:...</c> and <c>::{CLSID}</c>.</summary>
internal static class ShellForms
{
    public static bool IsShellForm(string target) =>
        target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("::{", StringComparison.OrdinalIgnoreCase);
}
