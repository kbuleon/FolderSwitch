// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using FolderSwitch.App.Views;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Web;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.App.Diagnostics.Snapshots;

/// <summary>
/// The services the view models of a snapshot are built with: they do nothing, save nothing, show nothing and open
/// nothing. A snapshot never touches the user's configuration, windows, files, keyboard, startup programs or the
/// network. A browser « shows » <see cref="BrowserPage"/>, and the favicon cache holds one file, made by the snapshot.
/// A folder a snapshot's context lacks is never read later. The icons of tabs and groups are the application's own
/// (<see cref="Icons"/>).
/// </summary>
internal sealed class SnapshotServices : IItemActivator, IConfigPersistence, IUserPrompts, IWindowHost, IBrowserAddressReader, ICurrentFolderReader, IFavicons, ISystemInfo, IHotkeyService, IAutostart
{
    public event EventHandler? ThemeChanged
    {
        add { }
        remove { }
    }

    public WindowsGeneration Generation => WindowsGeneration.Windows11;

    /// <summary>
    /// The emoji data of the icons, as the application has it: the files of the <c>Emoji</c> folder beside the
    /// executable, checked against the machine's emoji font. Loaded on first use, once for every view model.
    /// </summary>
    public Lazy<IconFinder> Icons { get; } = new(
        () => new IconFinder(EmojiLibrary.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Emoji")), new EmojiGlyphs()),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string UiCultureName => "fr-FR";

    /// <summary>Light: the variant is chosen by the appearance mode of each snapshot, never by the system.</summary>
    public bool AppsUseDarkTheme => false;

    public Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl) => Task.FromResult(ActivationOutcome.Done);

    public Task<ActivationOutcome> OpenInExplorerAsync(Item item) => Task.FromResult(ActivationOutcome.OpenedElsewhere);

    /// <summary>As on Windows 11: a folder, called from Explorer.</summary>
    public bool CtrlOpensNewTab(ItemKind kind, CallContext ctx) => ActivationRouter.CtrlOpensNewTab(ctx, kind, Generation);

    public void Save(AppConfig c)
    {
    }

    public bool Confirm(string messageKey) => false;

    public void ShowError(string messageKey)
    {
    }

    public string? PickFolder() => null;

    public string? PickFile(string? initialFolder) => null;

    public string? PickExportPath() => null;

    public string? PickImportPath() => null;

    public string? AskUrl(string? prefill = null) => null;

    public void HideWindow()
    {
    }

    public FolderLocation? TryRead(CallContextKind kind, nint window) => null;

    /// <summary>No file is ever selected: a snapshot reads nothing from Explorer.</summary>
    public ExplorerSelection? TryReadSelection(nint explorerWindow) => ExplorerSelection.NoFile;

    /// <summary>The page every browser shows: what the Add menu of a snapshot reads, at once.</summary>
    public static BrowserPage BrowserPage { get; } =
        new("https://www.morningstar.fr/fr/funds/snapshot/default.aspx?id=F00000QW1N", "Fonds Actions Europe – Synthèse");

    public BrowserPage? TryRead(nint window, BrowserKind browser) => BrowserPage;

    // ----- IFavicons: no network; the cache holds one file, for SnapshotSample.FaviconHost. -----

    /// <summary>The favicon of <see cref="SnapshotSample.FaviconHost"/>; <c>null</c>: every site shows the globe.</summary>
    public string? FaviconFile { get; set; }

    public string? CachedPath(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && string.Equals(uri.Host, SnapshotSample.FaviconHost, StringComparison.OrdinalIgnoreCase)
            ? FaviconFile
            : null;

    public Task<string?> FetchAsync(string url, CancellationToken ct) => Task.FromResult<string?>(null);

    // ----- IHotkeyService: no keyboard hook, no registration. -----

    /// <summary>
    /// What the next capture « presses », at once, so that a snapshot can show a combination of its choice in an
    /// editor; <c>null</c>: a capture waits forever.
    /// </summary>
    public Hotkey? CaptureResult { get; set; }

    event EventHandler? IHotkeyService.Triggered
    {
        add { }
        remove { }
    }

    public IReadOnlyList<HotkeyIssue> Register(Hotkey h) => HotkeyRules.Validate(h);

    public void Unregister()
    {
    }

    public IDisposable BeginCapture(Action<Hotkey> onCaptured)
    {
        if (CaptureResult is { } captured)
        {
            onCaptured(captured);
        }

        return new NoCapture();
    }

    public bool Probe(Hotkey h) => true;

    public void Dispose()
    {
    }

    // ----- IAutostart: shown as set, never written. -----

    public bool IsEnabled => true;

    public void Set(bool enabled, string exePath)
    {
    }

    private sealed class NoCapture : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
