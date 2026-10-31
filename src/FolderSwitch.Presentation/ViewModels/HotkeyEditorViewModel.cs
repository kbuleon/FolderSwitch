using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// Editing the global shortcut, in the wizard and in the settings: the combination shown, its problems, the
/// keyboard capture of a new one and the test zone. Edits nothing by itself: whoever owns it applies
/// <see cref="Current"/> (see <see cref="SettingsViewModel.ApplyHotkey"/>). Meant to be used from the UI thread,
/// capture callbacks included.
/// </summary>
public sealed partial class HotkeyEditorViewModel : ObservableObject, IDisposable
{
    private readonly IHotkeyService _hotkeys;
    private readonly Localizer _localizer;
    private readonly IKeyNames? _keyNames;

    private Hotkey _current;
    private Hotkey? _inEffect;
    private IReadOnlyList<HotkeyIssue> _issues;
    private IReadOnlyList<HotkeyIssueMessage> _messages;
    private IDisposable? _capture;
    private int _captureGeneration;
    private bool _captureFailed;
    private bool _disposed;

    /// <param name="keyNames">The names the keyboard layout gives keys (<c>null</c>: standard English key-cap names).</param>
    public HotkeyEditorViewModel(Hotkey initial, IHotkeyService hotkeys, Localizer localizer, IKeyNames? keyNames = null)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(localizer);

        _hotkeys = hotkeys;
        _localizer = localizer;
        _keyNames = keyNames;
        _current = initial;
        _issues = FindIssues(initial);
        _messages = BuildMessages();

        _localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>The combination being edited.</summary>
    public Hotkey Current => _current;

    /// <summary><see cref="Current"/> as the user reads it, e.g. <c>Ctrl + ⊞ Win</c>, in the current language.</summary>
    public string Display => Format(_current);

    /// <summary>
    /// The problems of <see cref="Current"/>: those of <see cref="HotkeyRules.Validate"/>, then
    /// <see cref="HotkeyIssue.TakenByOtherApp"/> when another application owns the combination. Only a combination
    /// with a regular key and no blocking problem yet is probed: a modifier-only one reserves nothing, and one
    /// already refused needs no second reason. The combination FolderSwitch itself registered is not taken, nor is
    /// one whose probe failed (the registration will tell).
    /// </summary>
    public IReadOnlyList<HotkeyIssue> Issues => _issues;

    /// <summary><see cref="Issues"/> explained in the current language, in the same order.</summary>
    public IReadOnlyList<HotkeyIssueMessage> Messages => _messages;

    /// <summary>True when <see cref="Current"/> has no blocking problem (warnings allowed).</summary>
    public bool CanApply => !_issues.Any(HotkeyRules.IsBlocking);

    /// <summary>True while the next combination pressed is being captured.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    public partial bool IsCapturing { get; private set; }

    /// <summary>
    /// The test zone saw the shortcut fire; cleared when <see cref="Current"/> or the shortcut in effect changes, and by
    /// <see cref="ResetTest"/>.
    /// </summary>
    [ObservableProperty]
    public partial bool TestRecognized { get; private set; }

    /// <summary>
    /// Whether the test zone can test <see cref="Current"/>: a press of the shortcut can only be the shortcut in effect.
    /// False in the settings while the combination shown is not applied yet; always true in the wizard, which registers
    /// the combination shown for its test.
    /// </summary>
    public bool CanTest => _inEffect is null || _inEffect == _current;

    /// <summary>Label of the reset button, e.g. « Reset to Ctrl + ⊞ Win ».</summary>
    public string ResetLabel => _localizer.Format("Hotkey.Reset", Format(Hotkey.Default));

    /// <summary>
    /// Invitation of the test zone, e.g. « Try the shortcut: press Ctrl + ⊞ Win »; while <see cref="CanTest"/> is false,
    /// the invitation to apply the combination first.
    /// </summary>
    public string TestPrompt => CanTest ? _localizer.Format("Hotkey.Test", Display) : _localizer["Hotkey.ApplyToTest"];

    /// <summary>
    /// Why the last « Change » could not start a capture (<c>Error.ActionFailed</c>, in the current language);
    /// <c>null</c> otherwise. Cleared by the next capture that starts and when <see cref="Current"/> changes.
    /// </summary>
    public string? CaptureError => _captureFailed ? _localizer["Error.ActionFailed"] : null;

    /// <summary>
    /// « Change »: captures the next combination the user presses, which becomes <see cref="Current"/>. The capture
    /// ends by itself after one combination, or with <see cref="StopCapture"/>. When the service cannot capture
    /// (keyboard hook refused, service disposed), nothing is captured and <see cref="CaptureError"/> says so.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    public void StartCapture()
    {
        if (!CanStartCapture())
        {
            return;
        }

        var generation = ++_captureGeneration;
        IDisposable capture;
        try
        {
            capture = _hotkeys.BeginCapture(captured => OnCaptured(generation, captured));
        }
        catch (Exception e) when (IsServiceFailure(e))
        {
            _captureGeneration++;
            SetCaptureFailed(true);
            return;
        }

        SetCaptureFailed(false);
        if (generation != _captureGeneration)
        {
            // The combination arrived before BeginCapture returned: the capture is already over.
            capture.Dispose();
            return;
        }

        _capture = capture;
        IsCapturing = true;
    }

    /// <summary>
    /// Ends the capture in progress without changing <see cref="Current"/>; a combination it would still deliver is
    /// dropped. The view calls it when its window loses the focus. Does nothing when no capture runs.
    /// </summary>
    [RelayCommand]
    public void StopCapture()
    {
        if (_capture is null)
        {
            return;
        }

        EndCapture();
    }

    /// <summary>« Reset to Ctrl + ⊞ Win »: ends any capture and goes back to the default shortcut.</summary>
    [RelayCommand]
    public void ResetToDefault()
    {
        StopCapture();
        SetCurrent(Hotkey.Default);
    }

    /// <summary>
    /// The shortcut in effect fired: the application calls this while the test zone is shown. It says « recognized »
    /// only when that is the combination shown (<see cref="CanTest"/>); otherwise the zone keeps asking to apply it.
    /// </summary>
    public void NotifyTriggered()
    {
        if (CanTest)
        {
            TestRecognized = true;
        }
    }

    /// <summary>The test zone starts again, waiting for a press (the settings panel was opened).</summary>
    public void ResetTest() => TestRecognized = false;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopCapture();
        _disposed = true;
        _localizer.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>Shows another combination (the settings after an import), ending any capture.</summary>
    internal void Show(Hotkey h)
    {
        ArgumentNullException.ThrowIfNull(h);

        StopCapture();
        SetCurrent(h);
    }

    /// <summary>
    /// The shortcut registered now, as its owner knows it (the settings, after the startup, an « Apply » or an import):
    /// the only one a press can come from, so the test zone tests only that one (<see cref="CanTest"/>). <c>null</c>
    /// (the default, the wizard's case): the combination shown is the one under test.
    /// </summary>
    internal void SetInEffect(Hotkey? h)
    {
        if (h == _inEffect)
        {
            return;
        }

        _inEffect = h;
        TestRecognized = false;
        OnPropertyChanged(nameof(CanTest));
        OnPropertyChanged(nameof(TestPrompt));
    }

    /// <summary>
    /// Checks <see cref="Current"/> again, probe included: after a registration was refused, the editor then shows
    /// why (taken by another application in the meantime, say).
    /// </summary>
    internal void Revalidate()
    {
        _issues = FindIssues(_current);
        _messages = BuildMessages();

        OnPropertyChanged(nameof(Issues));
        OnPropertyChanged(nameof(Messages));
        OnPropertyChanged(nameof(CanApply));
    }

    /// <summary>
    /// How <see cref="IHotkeyService"/> fails: a Win32 call refused (the keyboard hook, say), or the service
    /// disposed or shutting down (<see cref="ObjectDisposedException"/> is an <see cref="InvalidOperationException"/>).
    /// </summary>
    internal static bool IsServiceFailure(Exception e) => e is Win32Exception or InvalidOperationException;

    private bool CanStartCapture() => !_disposed && !IsCapturing;

    private void OnCaptured(int generation, Hotkey captured)
    {
        if (generation != _captureGeneration || _disposed)
        {
            return;
        }

        EndCapture();
        SetCurrent(captured);
    }

    private void EndCapture()
    {
        _captureGeneration++;
        var capture = _capture;
        _capture = null;
        IsCapturing = false;
        capture?.Dispose();
    }

    private void SetCurrent(Hotkey h)
    {
        _current = h;
        _issues = FindIssues(h);
        _messages = BuildMessages();
        TestRecognized = false;
        SetCaptureFailed(false);

        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(CanTest));
        OnPropertyChanged(nameof(TestPrompt));
        OnPropertyChanged(nameof(Issues));
        OnPropertyChanged(nameof(Messages));
        OnPropertyChanged(nameof(CanApply));
    }

    private IReadOnlyList<HotkeyIssue> FindIssues(Hotkey h)
    {
        var issues = HotkeyRules.Validate(h);
        if (h.IsModifierOnly || issues.Any(HotkeyRules.IsBlocking) || IsFree(h))
        {
            return issues;
        }

        return [.. issues, HotkeyIssue.TakenByOtherApp];
    }

    /// <summary>
    /// The probe is only a forecast: when it fails, the combination counts as free, and the registration itself
    /// (<see cref="SettingsViewModel.ApplyHotkey"/>, or the application at startup) reports the real problem.
    /// </summary>
    private bool IsFree(Hotkey h)
    {
        try
        {
            return _hotkeys.Probe(h);
        }
        catch (Exception e) when (IsServiceFailure(e))
        {
            return true;
        }
    }

    private void SetCaptureFailed(bool failed)
    {
        if (failed != _captureFailed)
        {
            _captureFailed = failed;
            OnPropertyChanged(nameof(CaptureError));
        }
    }

    private IReadOnlyList<HotkeyIssueMessage> BuildMessages() =>
        _issues
            .Select(issue => new HotkeyIssueMessage(issue, _localizer["Hotkey.Issue." + issue], HotkeyRules.IsBlocking(issue)))
            .ToList()
            .AsReadOnly();

    private string Format(Hotkey h) => HotkeyRules.Format(h, key => _localizer[key], _keyNames);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _messages = BuildMessages();
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(ResetLabel));
        OnPropertyChanged(nameof(TestPrompt));
        OnPropertyChanged(nameof(Messages));
        if (_captureFailed)
        {
            OnPropertyChanged(nameof(CaptureError));
        }
    }
}
