using System.ComponentModel;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// A registration for the wizard's test zone that could not be made: the combination, the problems the service found
/// (<see cref="HotkeyIssue.TakenByOtherApp"/>, say), or the error it failed with.
/// </summary>
public sealed record HotkeyTestRefusal(Hotkey Hotkey, IReadOnlyList<HotkeyIssue> Issues, Exception? Error);

/// <summary>
/// The test zone of the wizard's shortcut step. The wizard registers nothing before <see cref="WizardViewModel.Finish"/>,
/// so the shortcut shown would never fire: while <see cref="WizardViewModel.HotkeyStep"/> is shown and the shortcut has
/// no blocking problem, it is registered for the time being, and its trigger goes to the test zone
/// (<see cref="HotkeyEditorViewModel.NotifyTriggered"/>). It is registered again whenever it changes, and released when
/// the step is left, when the wizard finishes and by <see cref="Dispose"/> (the wizard closed).
/// </summary>
/// <remarks>
/// <para>
/// Each registration replaces the previous one, which is released first: a combination refused must not leave the
/// previous one in effect, whose trigger would say « recognized » for a shortcut no longer shown. A registration refused
/// or failed leaves nothing registered, and is told by <see cref="Refused"/>; the test zone then waits in vain, as for a
/// combination another application took in the meantime.
/// </para>
/// <para>
/// The service must have nothing else registered meanwhile: the wizard comes before the application's own shortcut.
/// UI thread only, <see cref="IHotkeyService.Triggered"/> included.
/// </para>
/// </remarks>
public sealed class WizardHotkeyTest : IDisposable
{
    private readonly WizardViewModel _wizard;
    private readonly IHotkeyService _hotkeys;
    private Hotkey? _refused;
    private bool _finished;
    private bool _disposed;

    public WizardHotkeyTest(WizardViewModel wizard, IHotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(wizard);
        ArgumentNullException.ThrowIfNull(hotkeys);

        _wizard = wizard;
        _hotkeys = hotkeys;

        _wizard.PropertyChanged += OnWizardChanged;
        _wizard.Hotkey.PropertyChanged += OnHotkeyChanged;
        _wizard.Finished += OnFinished;
        _hotkeys.Triggered += OnTriggered;
        Update();
    }

    /// <summary>A registration for the test could not be made (for the log: the test zone simply waits).</summary>
    public event EventHandler<HotkeyTestRefusal>? Refused;

    /// <summary>The shortcut registered for the test now; <c>null</c> while none is.</summary>
    public Hotkey? Registered { get; private set; }

    /// <summary>Releases the shortcut registered for the test, if any, and stops following the wizard.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hotkeys.Triggered -= OnTriggered;
        _wizard.Finished -= OnFinished;
        _wizard.Hotkey.PropertyChanged -= OnHotkeyChanged;
        _wizard.PropertyChanged -= OnWizardChanged;
        Release();
    }

    /// <summary>
    /// Registers the shortcut the test needs, if it is not already: the one shown, while its step is and it has no
    /// blocking problem; none otherwise. A combination refused is not tried again (the editor tells of one change in
    /// several notifications) until another one is wanted, or the step is shown anew.
    /// </summary>
    private void Update()
    {
        var wanted = !_disposed && !_finished && _wizard.Step == WizardViewModel.HotkeyStep && _wizard.Hotkey.CanApply
            ? _wizard.Hotkey.Current
            : null;
        if (wanted != _refused)
        {
            // Another combination is wanted, or none: the one refused may be tried again later.
            _refused = null;
        }

        if (wanted == Registered || (wanted is not null && wanted == _refused))
        {
            return;
        }

        Release();
        if (wanted is null)
        {
            return;
        }

        IReadOnlyList<HotkeyIssue> issues;
        try
        {
            issues = _hotkeys.Register(wanted);
        }
        catch (Exception e) when (HotkeyEditorViewModel.IsServiceFailure(e))
        {
            _refused = wanted;
            Refused?.Invoke(this, new HotkeyTestRefusal(wanted, [], e));
            return;
        }

        if (issues.Any(HotkeyRules.IsBlocking))
        {
            _refused = wanted;
            Refused?.Invoke(this, new HotkeyTestRefusal(wanted, issues, null));
            return;
        }

        Registered = wanted;
    }

    private void Release()
    {
        if (Registered is null)
        {
            return;
        }

        Registered = null;
        try
        {
            _hotkeys.Unregister();
        }
        catch (Exception e) when (HotkeyEditorViewModel.IsServiceFailure(e))
        {
            // The service is gone (disposed, shutting down): nothing of it is left registered.
        }
    }

    private void OnWizardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(WizardViewModel.Step))
        {
            Update();
        }
    }

    private void OnHotkeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(HotkeyEditorViewModel.Current) or nameof(HotkeyEditorViewModel.CanApply))
        {
            Update();
        }
    }

    private void OnFinished(object? sender, AppConfig e)
    {
        _finished = true;
        Release();
    }

    /// <summary>The test zone says « recognized » only for the shortcut it shows, and only on its step.</summary>
    private void OnTriggered(object? sender, EventArgs e)
    {
        if (!_disposed && Registered is { } tested && _wizard.Step == WizardViewModel.HotkeyStep && tested == _wizard.Hotkey.Current)
        {
            _wizard.Hotkey.NotifyTriggered();
        }
    }
}
