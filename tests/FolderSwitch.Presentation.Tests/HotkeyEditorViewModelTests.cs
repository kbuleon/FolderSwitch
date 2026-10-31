using System.ComponentModel;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public class HotkeyEditorViewModelTests
{
    private static readonly Hotkey CtrlShiftF = new(KeyModifiers.Ctrl | KeyModifiers.Shift, 'F');
    private static readonly Hotkey CtrlShiftSpace = new(KeyModifiers.Ctrl | KeyModifiers.Shift, VirtualKeys.Space);
    private static readonly Hotkey WinL = new(KeyModifiers.Win, VirtualKeys.L);
    private static readonly Hotkey CtrlAltF = new(KeyModifiers.Ctrl | KeyModifiers.Alt, 'F');
    private static readonly Hotkey CtrlShift = new(KeyModifiers.Ctrl | KeyModifiers.Shift, null);

    private readonly FakeHotkeyService _hotkeys = new();
    private readonly Localizer _localizer = TestLocalizer.Create();

    private HotkeyEditorViewModel Create(Hotkey? initial = null) => new(initial ?? Hotkey.Default, _hotkeys, _localizer);

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Starts_with_the_initial_hotkey()
    {
        using var vm = Create();

        Assert.Equal(Hotkey.Default, vm.Current);
        Assert.Equal("Ctrl + ⊞ Win", vm.Display);
        Assert.Empty(vm.Issues);
        Assert.True(vm.CanApply);
        Assert.False(vm.IsCapturing);
        Assert.False(vm.TestRecognized);
        Assert.Equal("Reset to Ctrl + ⊞ Win", vm.ResetLabel);
        Assert.Equal("Try the shortcut: press Ctrl + ⊞ Win", vm.TestPrompt);
    }

    [Fact]
    public void Capture_sets_the_current_hotkey_and_ends()
    {
        using var vm = Create();

        vm.StartCaptureCommand.Execute(null);
        Assert.True(vm.IsCapturing);
        Assert.False(vm.StartCaptureCommand.CanExecute(null));

        var changes = RecordChanges(vm);
        _hotkeys.Press(CtrlShiftF);

        Assert.Equal(CtrlShiftF, vm.Current);
        Assert.Equal("Ctrl + Shift + F", vm.Display);
        Assert.False(vm.IsCapturing);
        Assert.True(vm.StartCaptureCommand.CanExecute(null));
        Assert.True(_hotkeys.LastHandleDisposed);
        Assert.Contains(nameof(HotkeyEditorViewModel.Current), changes);
        Assert.Contains(nameof(HotkeyEditorViewModel.Display), changes);
        Assert.Contains(nameof(HotkeyEditorViewModel.Issues), changes);
        Assert.Contains(nameof(HotkeyEditorViewModel.CanApply), changes);
    }

    [Fact]
    public void Capture_delivered_before_BeginCapture_returns_is_kept()
    {
        _hotkeys.CaptureImmediately = CtrlShiftF;
        using var vm = Create();

        vm.StartCapture();

        Assert.Equal(CtrlShiftF, vm.Current);
        Assert.False(vm.IsCapturing);
        Assert.True(_hotkeys.LastHandleDisposed);
    }

    [Fact]
    public void Starting_a_capture_twice_starts_one()
    {
        using var vm = Create();

        vm.StartCapture();
        vm.StartCapture();

        Assert.Equal(1, _hotkeys.CaptureCount);
    }

    [Fact]
    public void StopCapture_ends_the_capture_and_drops_a_late_combination()
    {
        using var vm = Create();
        vm.StartCapture();

        vm.StopCapture();

        Assert.False(vm.IsCapturing);
        Assert.True(_hotkeys.LastHandleDisposed);

        _hotkeys.LastCallback!(CtrlShiftF);
        Assert.Equal(Hotkey.Default, vm.Current);
    }

    /// <summary>How the real service fails: hook not installed, service disposed, service shutting down.</summary>
    private static Exception Failure(string kind) => kind switch
    {
        "win32" => new Win32Exception(5),
        "disposed" => new ObjectDisposedException("HotkeyService"),
        _ => new InvalidOperationException(),
    };

    [Theory]
    [InlineData("win32")]
    [InlineData("disposed")]
    [InlineData("invalid")]
    public void Capture_that_cannot_start_is_reported(string failure)
    {
        _hotkeys.BeginCaptureThrows = Failure(failure);
        using var vm = Create();
        var changes = RecordChanges(vm);

        vm.StartCaptureCommand.Execute(null);

        Assert.False(vm.IsCapturing);
        Assert.True(vm.StartCaptureCommand.CanExecute(null));
        Assert.Equal("The action could not be completed.", vm.CaptureError);
        Assert.Contains(nameof(HotkeyEditorViewModel.CaptureError), changes);
        Assert.Equal(Hotkey.Default, vm.Current);

        _localizer.SetLanguage("fr");
        Assert.Equal("L'action n'a pas pu être effectuée.", vm.CaptureError);

        // The next attempt that works clears the message.
        _hotkeys.BeginCaptureThrows = null;
        vm.StartCapture();
        Assert.True(vm.IsCapturing);
        Assert.Null(vm.CaptureError);
    }

    [Fact]
    public void Capture_error_is_cleared_when_the_hotkey_changes()
    {
        _hotkeys.BeginCaptureThrows = new Win32Exception(5);
        using var vm = Create(CtrlShiftF);
        vm.StartCapture();
        Assert.NotNull(vm.CaptureError);

        vm.ResetToDefault();

        Assert.Null(vm.CaptureError);
    }

    [Theory]
    [InlineData("win32")]
    [InlineData("disposed")]
    [InlineData("invalid")]
    public void Probe_that_fails_counts_as_not_taken(string failure)
    {
        _hotkeys.ProbeThrows = Failure(failure);

        using var vm = Create(CtrlShiftF);

        Assert.Empty(vm.Issues);
        Assert.True(vm.CanApply);
    }

    [Fact]
    public void ResetToDefault_restores_ctrl_win_and_stops_capturing()
    {
        using var vm = Create(CtrlShiftF);
        vm.StartCapture();

        vm.ResetToDefaultCommand.Execute(null);

        Assert.Equal(Hotkey.Default, vm.Current);
        Assert.False(vm.IsCapturing);
        Assert.True(_hotkeys.LastHandleDisposed);
    }

    [Fact]
    public void Taken_by_another_application_is_found_by_probing()
    {
        _hotkeys.Taken.Add(CtrlShiftF);

        using var vm = Create(CtrlShiftF);

        Assert.Equal([HotkeyIssue.TakenByOtherApp], vm.Issues);
        Assert.False(vm.CanApply);
        Assert.Equal([CtrlShiftF], _hotkeys.Probed);
    }

    [Fact]
    public void The_hotkey_this_application_registered_is_not_taken()
    {
        _hotkeys.Register(CtrlShiftF);
        _hotkeys.Taken.Add(CtrlShiftF); // the system reports it as owned: by FolderSwitch itself

        using var vm = Create(CtrlShiftF);

        Assert.Empty(vm.Issues);
        Assert.True(vm.CanApply);
    }

    [Fact]
    public void Modifier_only_hotkeys_are_not_probed()
    {
        using var vm = Create(CtrlShift);

        Assert.Empty(vm.Issues);
        Assert.Empty(_hotkeys.Probed);
    }

    [Fact]
    public void A_hotkey_already_refused_is_not_probed()
    {
        _hotkeys.Taken.Add(WinL);

        using var vm = Create(WinL);

        Assert.Equal([HotkeyIssue.ReservedByWindows], vm.Issues);
        Assert.Empty(_hotkeys.Probed);
    }

    [Fact]
    public void Blocking_issues_forbid_applying_and_warnings_do_not()
    {
        using var vm = Create(new Hotkey(KeyModifiers.Ctrl, null));
        Assert.Equal([HotkeyIssue.SingleModifier], vm.Issues);
        Assert.False(vm.CanApply);

        vm.StartCapture();
        _hotkeys.Press(CtrlAltF);

        Assert.Equal([HotkeyIssue.CtrlAltLikeAltGr], vm.Issues);
        Assert.True(vm.CanApply);
    }

    [Fact]
    public void Messages_explain_each_issue_in_the_current_language()
    {
        using var vm = Create(WinL);

        var message = Assert.Single(vm.Messages);
        Assert.Equal(new HotkeyIssueMessage(HotkeyIssue.ReservedByWindows, "This shortcut is reserved by Windows.", IsBlocking: true), message);

        vm.StartCapture();
        _hotkeys.Press(CtrlAltF);
        Assert.Equal(
            [new HotkeyIssueMessage(HotkeyIssue.CtrlAltLikeAltGr, "Ctrl + Alt is the same as AltGr.", IsBlocking: false)],
            vm.Messages);
    }

    [Fact]
    public void Texts_follow_the_language()
    {
        using var vm = Create(CtrlShiftSpace);
        var changes = RecordChanges(vm);

        _localizer.SetLanguage("fr");

        Assert.Equal("Ctrl + Maj + Espace", vm.Display);
        Assert.Equal("Revenir à Ctrl + ⊞ Win", vm.ResetLabel);
        Assert.Equal("Essayez le raccourci : appuyez sur Ctrl + Maj + Espace", vm.TestPrompt);
        Assert.Contains(nameof(HotkeyEditorViewModel.Display), changes);
        Assert.Contains(nameof(HotkeyEditorViewModel.ResetLabel), changes);
        Assert.Contains(nameof(HotkeyEditorViewModel.TestPrompt), changes);
        Assert.Contains(nameof(HotkeyEditorViewModel.Messages), changes);
    }

    [Fact]
    public void Test_zone_confirms_recognition_until_the_hotkey_changes()
    {
        using var vm = Create();

        vm.NotifyTriggered();
        Assert.True(vm.TestRecognized);

        vm.StartCapture();
        _hotkeys.Press(CtrlShiftF);
        Assert.False(vm.TestRecognized);
    }

    [Fact]
    public void Without_a_shortcut_in_effect_the_test_zone_tests_the_combination_shown()
    {
        // The wizard's case: it registers the combination shown for its test.
        using var vm = Create();
        vm.StartCapture();
        _hotkeys.Press(CtrlShiftF);

        Assert.True(vm.CanTest);
        Assert.False(vm.IsInEffect);
        Assert.Equal("Try the shortcut: press Ctrl + Shift + F", vm.TestPrompt);
        vm.NotifyTriggered();
        Assert.True(vm.TestRecognized);
    }

    [Fact]
    public void ResetTest_makes_the_test_zone_wait_again()
    {
        using var vm = Create();
        vm.NotifyTriggered();

        vm.ResetTest();

        Assert.False(vm.TestRecognized);
        Assert.Equal("Try the shortcut: press Ctrl + ⊞ Win", vm.TestPrompt);
    }

    [Fact]
    public void Keys_are_named_by_the_layout_when_names_are_given()
    {
        using var vm = new HotkeyEditorViewModel(
            new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 0x24), _hotkeys, _localizer, new LayoutNames());

        Assert.Equal("Ctrl + Alt + Origine", vm.Display);
    }

    [Fact]
    public void Keys_get_their_key_cap_names_without_layout_names()
    {
        using var vm = Create(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 0x24));

        Assert.Equal("Ctrl + Alt + Home", vm.Display);
    }

    [Fact]
    public void Dispose_ends_the_capture_and_stops_following_the_language()
    {
        var vm = Create();
        vm.StartCapture();

        vm.Dispose();

        Assert.True(_hotkeys.LastHandleDisposed);
        var changes = RecordChanges(vm);
        _localizer.SetLanguage("fr");
        Assert.Empty(changes);
    }

    private sealed class LayoutNames : IKeyNames
    {
        public string? NameOf(int virtualKey) => virtualKey == 0x24 ? "Origine" : null;
    }
}
