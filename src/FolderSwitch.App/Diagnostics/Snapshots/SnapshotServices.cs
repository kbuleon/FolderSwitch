using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.App.Diagnostics.Snapshots;

/// <summary>
/// The services the view models of a snapshot are built with: they do nothing, save nothing, show nothing and open
/// nothing. A snapshot never touches the user's configuration, windows, files, keyboard or startup programs.
/// </summary>
internal sealed class SnapshotServices : IItemActivator, IConfigPersistence, IUserPrompts, IWindowHost, ISystemInfo, IHotkeyService, IAutostart
{
    public event EventHandler? ThemeChanged
    {
        add { }
        remove { }
    }

    public WindowsGeneration Generation => WindowsGeneration.Windows11;

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

    public string? PickFile() => null;

    public string? PickExportPath() => null;

    public string? PickImportPath() => null;

    public void HideWindow()
    {
    }

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
