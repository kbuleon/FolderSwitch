// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace DialogHost;

/// <summary>The dialog kinds DialogHost can open.</summary>
public enum DialogMode
{
    Open,
    Save,
    Folder,
    LegacyOpen,
    LegacySave,
    LegacyFolder,
    LegacyFolderClassic,
}

/// <summary>The parsed command line: <c>&lt;mode&gt; &lt;initialFolder&gt; [fileName]</c>.</summary>
public sealed record DialogRequest(DialogMode Mode, string InitialFolder, string? FileName)
{
    private static readonly Dictionary<string, DialogMode> Modes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = DialogMode.Open,
        ["save"] = DialogMode.Save,
        ["folder"] = DialogMode.Folder,
        ["legacy-open"] = DialogMode.LegacyOpen,
        ["legacy-save"] = DialogMode.LegacySave,
        ["legacy-folder"] = DialogMode.LegacyFolder,
        ["legacy-folder-classic"] = DialogMode.LegacyFolderClassic,
    };

    public static bool TryParse(string[] args, out DialogRequest request, out string error)
    {
        request = null!;
        if (args.Length is < 2 or > 3)
        {
            error = "usage: DialogHost <" + string.Join('|', Modes.Keys) + "> <initialFolder> [fileName]";
            return false;
        }

        if (!Modes.TryGetValue(args[0], out var mode))
        {
            error = "unknown mode '" + args[0] + "'";
            return false;
        }

        request = new DialogRequest(mode, args[1], args.Length == 3 ? args[2] : null);
        error = "";
        return true;
    }
}
