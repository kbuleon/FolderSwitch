using System.Runtime.InteropServices;

namespace FolderSwitch.Windows.Shell;

/// <summary>
/// Turns what a dialog displays (folder display names) back into a parsing name, by walking the shell
/// namespace from the desktop. Requires an STA thread (shell folders are apartment-threaded objects).
/// </summary>
internal static class ShellNamespace
{
    /// <summary>Children compared at most per level, so that a huge folder cannot stall the walk.</summary>
    private const int MaxChildrenPerLevel = 2000;

    private static readonly Guid ShellFolderIid = typeof(IShellFolder).GUID;

    private static readonly uint[] NameKinds = [ShellNative.ShgdnInFolder, ShellNative.ShgdnNormal];

    /// <summary><c>SFGAO_HIDDEN | SFGAO_SYSTEM</c>.</summary>
    private const uint SuperHidden = 0x00080000 | 0x00001000;

    /// <summary>
    /// The parsing name of the folder reached by following display names from the desktop, such as
    /// <c>["Desktop", "This PC", "Local Disk (C:)", "Users"]</c> (a folder-picker tree) or
    /// <c>["This PC"]</c> (an address bar). A leading segment naming the desktop itself is skipped.
    /// <c>null</c> if a segment matches nothing.
    /// </summary>
    /// <remarks>
    /// Each segment is first parsed as a child name of the current folder (a file-system folder whose
    /// display name is its name, the usual case), then looked up among the subfolders, hidden ones
    /// included, by display name (a drive, a virtual folder, a localized name like "Utilisateurs").
    /// </remarks>
    public static string? ResolveDisplayPath(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0 || ShellNative.SHGetDesktopFolder(out var desktop) != ShellNative.S_OK || desktop is null)
        {
            return null;
        }

        // The empty ID list: the desktop, root of the namespace.
        var absolute = Marshal.AllocCoTaskMem(sizeof(ushort));
        Marshal.WriteInt16(absolute, 0);
        var folder = desktop;
        try
        {
            var first = 0;
            if (DisplayNames(desktop, absolute).Contains(segments[0], StringComparer.OrdinalIgnoreCase))
            {
                first = 1;
            }

            for (var i = first; i < segments.Count; i++)
            {
                var child = FindChild(folder, segments[i]);
                if (child == 0)
                {
                    return null;
                }

                try
                {
                    var combined = ShellNative.ILCombine(absolute, child);
                    if (combined == 0)
                    {
                        return null;
                    }

                    Marshal.FreeCoTaskMem(absolute);
                    absolute = combined;

                    if (i < segments.Count - 1)
                    {
                        if (folder.BindToObject(child, 0, ShellFolderIid, out var next) != ShellNative.S_OK
                            || next is not IShellFolder nextFolder)
                        {
                            ShellNative.Release(next);
                            return null;
                        }

                        if (!ReferenceEquals(folder, desktop))
                        {
                            ShellNative.Release(folder);
                        }

                        folder = nextFolder;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(child);
                }
            }

            return ShellNative.ParsingName(absolute);
        }
        finally
        {
            Marshal.FreeCoTaskMem(absolute);
            if (!ReferenceEquals(folder, desktop))
            {
                ShellNative.Release(folder);
            }

            ShellNative.Release(desktop);
        }
    }

    /// <summary>
    /// The child ID of <paramref name="folder"/> shown as <paramref name="displayName"/>, or zero. The
    /// caller frees it; any other ID met here is freed here, even when a call throws.
    /// </summary>
    private static nint FindChild(IShellFolder folder, string displayName)
    {
        // Asked for along with the parse: hidden and system together ("super hidden") make an item the
        // enumeration below does not list either. Such an item, the legacy compatibility junction
        // "Start Menu\Programmes" for one (Hidden, System, ReparsePoint), parses under the localized
        // name of the real folder ("Programs", shown as "Programmes"): it is not that folder.
        var attributes = SuperHidden;
        if (folder.ParseDisplayName(0, 0, displayName, out _, out var parsed, ref attributes) == ShellNative.S_OK
            && parsed != 0
            && Keep(parsed, () => (attributes & SuperHidden) != SuperHidden && IsSingleLevel(parsed) && Matches(folder, parsed, displayName)))
        {
            // A single-level ID whose display name matches: a folder named exactly like that.
            return parsed;
        }

        if (folder.EnumObjects(0, ShellNative.ShcontfFolders | ShellNative.ShcontfIncludeHidden, out var children)
                != ShellNative.S_OK
            || children is null)
        {
            ShellNative.Release(children);
            return 0;
        }

        try
        {
            for (var n = 0; n < MaxChildrenPerLevel; n++)
            {
                if (children.Next(1, out var child, out var fetched) != ShellNative.S_OK || fetched != 1 || child == 0)
                {
                    return 0;
                }

                if (Keep(child, () => Matches(folder, child, displayName)))
                {
                    return child;
                }
            }

            return 0;
        }
        finally
        {
            ShellNative.Release(children);
        }
    }

    /// <summary>Whether <paramref name="test"/> holds for <paramref name="pidl"/>; if not, or if it throws, the ID is freed.</summary>
    private static bool Keep(nint pidl, Func<bool> test)
    {
        var keep = false;
        try
        {
            keep = test();
            return keep;
        }
        finally
        {
            if (!keep)
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
    }

    private static bool Matches(IShellFolder folder, nint child, string displayName) =>
        DisplayNames(folder, child).Contains(displayName, StringComparer.OrdinalIgnoreCase);

    /// <summary>The in-folder and normal display names of a child of <paramref name="folder"/>.</summary>
    private static IEnumerable<string> DisplayNames(IShellFolder folder, nint child)
    {
        foreach (var flags in NameKinds)
        {
            if (folder.GetDisplayNameOf(child, flags, out var name) == ShellNative.S_OK
                && ShellNative.StrRetToStrW(ref name, child, out var text) == ShellNative.S_OK
                && ShellNative.TakeString(text) is { Length: > 0 } displayName)
            {
                yield return displayName;
            }
        }
    }

    private static bool IsSingleLevel(nint pidl)
    {
        var size = (ushort)Marshal.ReadInt16(pidl);
        return size != 0 && Marshal.ReadInt16(pidl, size) == 0;
    }
}
