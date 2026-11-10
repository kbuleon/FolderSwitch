// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Tests.Activation;

public class ActivationServiceTests
{
    private const int ExplorerWindow = 100;
    private const int DialogWindow = 200;

    private static readonly CallContext ExplorerContext = new(CallContextKind.Explorer, ExplorerWindow, @"C:\Current");
    private static readonly CallContext DialogContext = new(CallContextKind.FileDialog, DialogWindow, @"C:\Current");
    private static readonly CallContext OtherContext = CallContext.None;

    private readonly FakeExplorerController _explorer = new();
    private readonly FakeFileDialogNavigator _dialog = new();
    private readonly FakeShellLauncher _shell = new();
    private readonly FakePathResolver _resolver = new();

    private ActivationService CreateService(WindowsGeneration os = WindowsGeneration.Windows11) =>
        new(_explorer, _dialog, _shell, _resolver, os);

    private ActivationService CreateService(Action<ActivationProblem> onProblem) =>
        new(_explorer, _dialog, _shell, _resolver, WindowsGeneration.Windows11, onProblem);

    private static Item Folder(string path) => new() { Kind = ItemKind.Folder, Path = path };

    private static Item File(string path) => new() { Kind = ItemKind.File, Path = path };

    private static Item Url(string? address) => new() { Kind = ItemKind.Url, Url = address };

    private void AssertNothingCalled()
    {
        Assert.Empty(_explorer.Calls);
        Assert.Empty(_dialog.Calls);
        Assert.Empty(_shell.Calls);
    }

    // A separator has nothing to open.

    [Fact]
    public async Task Separator_is_never_resolved_checked_nor_launched()
    {
        var resolved = 0;
        _resolver.ResolveFunc = _ =>
        {
            resolved++;
            return @"C:\Work";
        };
        var separator = new Item { Kind = ItemKind.Separator };
        var service = CreateService();

        Assert.Equal(ActivationOutcome.TargetUnavailable, await service.ActivateAsync(separator, ExplorerContext, ctrl: false));
        Assert.Equal(ActivationOutcome.TargetUnavailable, await service.ActivateAsync(separator, DialogContext, ctrl: true));
        Assert.Equal(ActivationOutcome.TargetUnavailable, await service.OpenInExplorerAsync(separator));
        Assert.False(service.CtrlOpensNewTab(ItemKind.Separator, ExplorerContext));

        Assert.Equal(0, resolved);
        Assert.Empty(_resolver.ExistsCalls);
        AssertNothingCalled();
    }

    // Resolution and existence check.

    [Fact]
    public async Task Unresolved_target_is_unavailable_and_no_controller_is_called()
    {
        _resolver.ResolveFunc = _ => null;

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        Assert.Empty(_resolver.ExistsCalls);
        AssertNothingCalled();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_resolved_target_is_unavailable(string resolved)
    {
        _resolver.ResolveFunc = _ => resolved;

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        AssertNothingCalled();
    }

    [Fact]
    public async Task Missing_target_is_unavailable_and_no_controller_is_called()
    {
        _resolver.ExistsFunc = (_, _) => Task.FromResult(false);

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Gone"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        Assert.Equal([(@"C:\Gone", TimeSpan.FromSeconds(2))], _resolver.ExistsCalls);
        AssertNothingCalled();
    }

    [Fact]
    public async Task Existence_check_that_throws_makes_the_target_unavailable()
    {
        _resolver.ExistsFunc = (_, _) => Task.FromException<bool>(new IOException("network drive offline"));

        var outcome = await CreateService().ActivateAsync(Folder(@"Z:\Share"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        AssertNothingCalled();
    }

    [Fact]
    public async Task Resolver_that_throws_gives_Failed_and_never_propagates()
    {
        _resolver.ResolveFunc = _ => throw new InvalidOperationException("boom");

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
        AssertNothingCalled();
    }

    [Fact]
    public async Task Existence_check_that_never_completes_gives_TargetUnavailable_within_2_5_seconds()
    {
        var never = new TaskCompletionSource<bool>();
        _resolver.ExistsFunc = (_, _) => never.Task;

        var stopwatch = Stopwatch.StartNew();
        var outcome = await CreateService().ActivateAsync(Folder(@"\\offline\share"), ExplorerContext, ctrl: false);
        stopwatch.Stop();

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2.5), $"took {stopwatch.Elapsed}");
        Assert.Equal([(@"\\offline\share", TimeSpan.FromSeconds(2))], _resolver.ExistsCalls);
        AssertNothingCalled();
    }

    [Fact]
    public async Task Existence_check_that_blocks_its_thread_does_not_block_the_caller_past_the_deadline()
    {
        using var release = new ManualResetEventSlim();
        _resolver.ExistsFunc = (_, _) =>
        {
            release.Wait(TimeSpan.FromSeconds(30)); // ignores the timeout and blocks synchronously
            return Task.FromResult(true);
        };

        var stopwatch = Stopwatch.StartNew();
        var activation = CreateService().ActivateAsync(Folder(@"\\offline\share"), OtherContext, ctrl: false);
        var outcome = await activation;
        stopwatch.Stop();
        release.Set();

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2.5), $"took {stopwatch.Elapsed}");
    }

    [Theory]
    [InlineData("shell:MyComputerFolder")]
    [InlineData("SHELL:RecycleBinFolder")]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}")]
    public async Task Shell_forms_skip_the_existence_check(string target)
    {
        _resolver.ResolveFunc = _ => target;
        _resolver.ExistsFunc = (_, _) => Task.FromResult(false);

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\ignored"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Empty(_resolver.ExistsCalls);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, target)], _explorer.Calls);
    }

    [Fact]
    public async Task ThisPC_special_folder_opens_shell_MyComputerFolder_without_existence_check()
    {
        var item = new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.ThisPC };
        _resolver.ResolveFunc = i => i.Special == SpecialFolderKey.ThisPC ? "shell:MyComputerFolder" : null;
        _resolver.ExistsFunc = (_, _) => Task.FromResult(false);

        var outcome = await CreateService().ActivateAsync(item, OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Empty(_resolver.ExistsCalls);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, "shell:MyComputerFolder")], _explorer.Calls);
    }

    [Fact]
    public async Task ThisPC_in_an_explorer_window_navigates_that_window_to_the_shell_form()
    {
        var item = new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.ThisPC };
        _resolver.ResolveFunc = _ => "shell:MyComputerFolder";

        var outcome = await CreateService().ActivateAsync(item, ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Done, outcome);
        Assert.Empty(_resolver.ExistsCalls);
        Assert.Equal([new ExplorerCall("Navigate", ExplorerWindow, "shell:MyComputerFolder")], _explorer.Calls);
    }

    [Fact]
    public async Task Available_unc_folder_opens_in_a_new_window_elsewhere()
    {
        const string unc = @"\\server\share\projects";

        var outcome = await CreateService().ActivateAsync(Folder(unc), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([(unc, TimeSpan.FromSeconds(2))], _resolver.ExistsCalls);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, unc)], _explorer.Calls);
    }

    // Explorer context.

    [Fact]
    public async Task Explorer_click_navigates_the_explorer_window()
    {
        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Done, outcome);
        Assert.Equal([new ExplorerCall("Navigate", ExplorerWindow, @"C:\Work")], _explorer.Calls);
        Assert.Empty(_dialog.Calls);
        Assert.Empty(_shell.Calls);
    }

    [Fact]
    public async Task Explorer_navigation_that_returns_false_falls_back_to_a_new_window()
    {
        // The calling window was closed meanwhile, say.
        _explorer.NavigateResult = false;

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal(
            [
                new ExplorerCall("Navigate", ExplorerWindow, @"C:\Work"),
                new ExplorerCall("OpenNewWindow", 0, @"C:\Work"),
            ],
            _explorer.Calls);
    }

    [Fact]
    public async Task Explorer_navigation_and_new_window_that_both_fail_give_Failed()
    {
        _explorer.NavigateResult = false;
        _explorer.OpenNewWindowException = new InvalidOperationException("cannot start explorer");

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Explorer_ctrl_click_on_Windows11_opens_a_new_tab()
    {
        var outcome = await CreateService(WindowsGeneration.Windows11)
            .ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.Done, outcome);
        Assert.Equal([new ExplorerCall("OpenInNewTab", ExplorerWindow, @"C:\Work")], _explorer.Calls);
    }

    [Fact]
    public async Task Failed_new_tab_falls_back_to_a_new_window_elsewhere()
    {
        _explorer.OpenInNewTabResult = false;

        var outcome = await CreateService(WindowsGeneration.Windows11)
            .ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal(
            [
                new ExplorerCall("OpenInNewTab", ExplorerWindow, @"C:\Work"),
                new ExplorerCall("OpenNewWindow", 0, @"C:\Work"),
            ],
            _explorer.Calls);
    }

    [Fact]
    public async Task Failed_new_tab_then_failing_new_window_gives_Failed()
    {
        _explorer.OpenInNewTabResult = false;
        _explorer.OpenNewWindowException = new InvalidOperationException("cannot start explorer");

        var outcome = await CreateService(WindowsGeneration.Windows11)
            .ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task New_tab_that_throws_counts_as_failed_and_falls_back_to_a_new_window()
    {
        _explorer.OpenInNewTabException = new InvalidOperationException("UI automation error");

        var outcome = await CreateService(WindowsGeneration.Windows11)
            .ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal(
            [
                new ExplorerCall("OpenInNewTab", ExplorerWindow, @"C:\Work"),
                new ExplorerCall("OpenNewWindow", 0, @"C:\Work"),
            ],
            _explorer.Calls);
    }

    [Fact]
    public async Task New_tab_that_throws_then_failing_new_window_gives_Failed()
    {
        _explorer.OpenInNewTabException = new InvalidOperationException("UI automation error");
        _explorer.OpenNewWindowException = new InvalidOperationException("cannot start explorer");

        var outcome = await CreateService(WindowsGeneration.Windows11)
            .ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.Failed, outcome);
        Assert.Equal(
            [
                new ExplorerCall("OpenInNewTab", ExplorerWindow, @"C:\Work"),
                new ExplorerCall("OpenNewWindow", 0, @"C:\Work"),
            ],
            _explorer.Calls);
    }

    [Fact]
    public async Task Explorer_ctrl_click_on_Windows10_opens_a_new_window_without_trying_a_tab()
    {
        var outcome = await CreateService(WindowsGeneration.Windows10)
            .ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, @"C:\Work")], _explorer.Calls);
    }

    // File dialog context.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dialog_click_moves_the_dialog_to_the_folder(bool ctrl)
    {
        var outcome = await CreateService().ActivateAsync(Folder(@"D:\a"), DialogContext, ctrl);

        Assert.Equal(ActivationOutcome.Done, outcome);
        Assert.Equal([(DialogWindow, @"D:\a")], _dialog.Calls);
        Assert.Empty(_explorer.Calls);
        Assert.Empty(_shell.Calls);
    }

    [Fact]
    public async Task Dialog_navigation_that_returns_false_gives_Failed()
    {
        _dialog.NavigateResult = false;

        var outcome = await CreateService().ActivateAsync(Folder(@"D:\a"), DialogContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
        Assert.Equal([(DialogWindow, @"D:\a")], _dialog.Calls);
    }

    [Fact]
    public async Task Dialog_file_is_opened_with_the_shell_after_checking_the_file_itself()
    {
        // The dialog never offers a file (spec 5.7); a file reaching the service is opened like anywhere else.
        var outcome = await CreateService().ActivateAsync(File(@"D:\a\b.txt"), DialogContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([(@"D:\a\b.txt", TimeSpan.FromSeconds(2))], _resolver.ExistsCalls);
        Assert.Equal([@"D:\a\b.txt"], _shell.Calls);
        Assert.Empty(_dialog.Calls);
    }

    [Fact]
    public async Task Dialog_file_that_no_longer_exists_is_unavailable_even_if_its_folder_exists()
    {
        _resolver.ExistsFunc = (path, _) => Task.FromResult(path == @"D:\a");

        var outcome = await CreateService().ActivateAsync(File(@"D:\a\gone.txt"), DialogContext, ctrl: false);

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        AssertNothingCalled();
    }

    // Web sites (spec 5.7): nothing to resolve, nothing to check.

    [Fact]
    public async Task Url_is_opened_without_resolving_or_checking()
    {
        var resolved = false;
        _resolver.ResolveFunc = _ =>
        {
            resolved = true;
            return null;
        };

        var outcome = await CreateService().ActivateAsync(Url("https://example.com/page"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.False(resolved);
        Assert.Empty(_resolver.ExistsCalls);
        Assert.Equal(["https://example.com/page"], _shell.Calls);
        Assert.Empty(_explorer.Calls);
        Assert.Empty(_dialog.Calls);
    }

    [Theory]
    [InlineData("file:///C:/wiki/index.html", "file:///C:/wiki/index.html")]
    [InlineData("FILE://Server/share/a b.html", "file://server/share/a%20b.html")]
    [InlineData("ftp://Files.example.com/pub", "ftp://files.example.com/pub")]
    public async Task File_and_ftp_urls_are_handed_to_the_shell(string address, string expected)
    {
        var outcome = await CreateService().ActivateAsync(Url(address), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([expected], _shell.Calls);
    }

    [Fact]
    public async Task Url_is_normalized_before_it_is_launched()
    {
        var outcome = await CreateService().ActivateAsync(Url("  HTTPS://Example.com "), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal(["https://example.com/"], _shell.Calls);
    }

    [Theory]
    [InlineData("gopher://x")]
    [InlineData("javascript:alert(1)")]
    [InlineData(@"C:\Windows
otepad.exe")]
    [InlineData("ftp://user:pass@x/")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Url_item_with_invalid_address_is_unavailable(string? address)
    {
        var outcome = await CreateService().ActivateAsync(Url(address), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        AssertNothingCalled();
    }

    [Fact]
    public async Task Url_the_shell_could_not_open_is_reported_by_the_shell()
    {
        _shell.OpenUrlResult = false;

        var outcome = await CreateService().ActivateAsync(Url("https://example.com/"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.ReportedByShell, outcome);
    }

    [Fact]
    public async Task Url_launch_that_throws_gives_Failed_and_never_propagates()
    {
        _shell.OpenUrlException = new InvalidOperationException("boom");

        var outcome = await CreateService().ActivateAsync(Url("https://example.com/"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task OpenInExplorer_of_a_url_is_unavailable()
    {
        var outcome = await CreateService().OpenInExplorerAsync(Url("https://example.com/"));

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        AssertNothingCalled();
    }

    // Other context and files.

    [Fact]
    public async Task File_in_another_context_is_opened_with_the_shell()
    {
        var outcome = await CreateService().ActivateAsync(File(@"D:\a\b.txt"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([@"D:\a\b.txt"], _shell.Calls);
        Assert.Empty(_explorer.Calls);
        Assert.Empty(_dialog.Calls);
    }

    [Fact]
    public async Task File_in_an_explorer_window_is_opened_with_the_shell()
    {
        var outcome = await CreateService().ActivateAsync(File(@"D:\a\b.txt"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([@"D:\a\b.txt"], _shell.Calls);
        Assert.Empty(_explorer.Calls);
    }

    [Fact]
    public async Task Folder_in_another_context_opens_a_new_explorer_window()
    {
        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, @"C:\Work")], _explorer.Calls);
    }

    // Exceptions never reach the UI.

    [Fact]
    public async Task Exception_from_explorer_navigation_falls_back_to_a_new_window()
    {
        _explorer.NavigateException = new InvalidOperationException("COM error");

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
    }

    [Fact]
    public async Task Exception_from_explorer_navigation_then_from_the_new_window_gives_Failed()
    {
        _explorer.NavigateException = new InvalidOperationException("COM error");
        _explorer.OpenNewWindowException = new InvalidOperationException("cannot start explorer");

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Exception_from_new_window_gives_Failed()
    {
        _explorer.OpenNewWindowException = new InvalidOperationException("cannot start explorer");

        var outcome = await CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Exception_from_dialog_navigation_gives_Failed()
    {
        _dialog.NavigateException = new InvalidOperationException("UI automation error");

        var outcome = await CreateService().ActivateAsync(Folder(@"D:\a"), DialogContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Exception_from_the_shell_launcher_gives_Failed()
    {
        _shell.OpenFileException = new InvalidOperationException("no associated application");

        var outcome = await CreateService().ActivateAsync(File(@"D:\a\b.xyz"), OtherContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    [Fact]
    public async Task A_file_the_shell_could_not_open_and_said_so_is_ReportedByShell()
    {
        _shell.OpenFileResult = false;

        var outcome = await CreateService().ActivateAsync(File(@"D:\a\b.xyz"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.ReportedByShell, outcome);
        Assert.Equal([@"D:\a\b.xyz"], _shell.Calls);
    }

    // The item's menu.

    [Theory]
    [InlineData(CallContextKind.Explorer)]
    [InlineData(CallContextKind.FileDialog)]
    [InlineData(CallContextKind.Other)]
    public async Task Open_in_Explorer_opens_a_folder_in_a_new_window_whatever_the_call(CallContextKind _)
    {
        var outcome = await CreateService().OpenInExplorerAsync(Folder(@"C:\Work"));

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, @"C:\Work")], _explorer.Calls);
        Assert.Empty(_dialog.Calls);
        Assert.Empty(_shell.Calls);
    }

    [Fact]
    public async Task Open_in_Explorer_opens_the_folder_of_a_file_and_not_the_file()
    {
        var outcome = await CreateService().OpenInExplorerAsync(File(@"D:\a\b.txt"));

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        Assert.Equal([new ExplorerCall("OpenNewWindow", 0, @"D:\a")], _explorer.Calls);
        Assert.Empty(_shell.Calls);
    }

    [Fact]
    public async Task Open_in_Explorer_checks_the_target_first()
    {
        _resolver.ExistsFunc = (_, _) => Task.FromResult(false);

        var outcome = await CreateService().OpenInExplorerAsync(File(@"D:\a\gone.txt"));

        Assert.Equal(ActivationOutcome.TargetUnavailable, outcome);
        AssertNothingCalled();
    }

    [Theory]
    [InlineData(ItemKind.Folder, WindowsGeneration.Windows11, true)]
    [InlineData(ItemKind.Folder, WindowsGeneration.Windows10, false)]
    [InlineData(ItemKind.File, WindowsGeneration.Windows11, false)]
    public void CtrlOpensNewTab_follows_the_router(ItemKind kind, WindowsGeneration os, bool expected)
    {
        Assert.Equal(expected, CreateService(os).CtrlOpensNewTab(kind, ExplorerContext));
        Assert.False(CreateService(os).CtrlOpensNewTab(kind, OtherContext));
    }

    [Fact]
    public void ActivationService_is_usable_through_IItemActivator()
    {
        IItemActivator activator = CreateService();

        Assert.NotNull(activator);
    }

    // Critical exceptions are not swallowed.

    [Fact]
    public async Task Out_of_memory_in_an_action_is_let_through()
    {
        _dialog.NavigateException = new OutOfMemoryException();

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () => CreateService().ActivateAsync(Folder(@"D:\a"), DialogContext, ctrl: false));
    }

    [Fact]
    public async Task Out_of_memory_in_an_action_with_a_fallback_is_let_through_without_the_fallback()
    {
        _explorer.NavigateException = new OutOfMemoryException();

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () => CreateService().ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false));
        Assert.Equal([new ExplorerCall("Navigate", ExplorerWindow, @"C:\Work")], _explorer.Calls);
    }

    [Fact]
    public async Task A_critical_exception_wrapped_in_another_is_let_through()
    {
        _resolver.ResolveFunc = _ => throw new InvalidOperationException("wrapper", new InsufficientExecutionStackException());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: false));
    }

    [Fact]
    public async Task Out_of_memory_in_the_existence_check_is_let_through()
    {
        _resolver.ExistsFunc = (_, _) => Task.FromException<bool>(new OutOfMemoryException());

        await Assert.ThrowsAsync<OutOfMemoryException>(
            () => CreateService().ActivateAsync(Folder(@"C:\Work"), OtherContext, ctrl: false));
    }

    // What is reported for the log.

    [Fact]
    public async Task A_plain_success_reports_nothing()
    {
        var problems = new List<ActivationProblem>();

        var outcome = await CreateService(problems.Add).ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Done, outcome);
        Assert.Empty(problems);
    }

    [Fact]
    public async Task An_unavailable_target_is_reported_with_its_path_and_no_plan()
    {
        _resolver.ExistsFunc = (_, _) => Task.FromResult(false);
        var problems = new List<ActivationProblem>();

        await CreateService(problems.Add).ActivateAsync(Folder(@"C:\Gone"), ExplorerContext, ctrl: false);

        var problem = Assert.Single(problems);
        Assert.Equal(ActivationOutcome.TargetUnavailable, problem.Outcome);
        Assert.Equal(@"C:\Gone", problem.Target);
        Assert.Null(problem.Plan);
        Assert.Null(problem.Error);
    }

    [Fact]
    public async Task A_failed_action_is_reported_with_its_plan_and_exception()
    {
        var error = new InvalidOperationException("UI automation error");
        _dialog.NavigateException = error;
        var problems = new List<ActivationProblem>();

        var outcome = await CreateService(problems.Add).ActivateAsync(Folder(@"D:\a"), DialogContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
        var problem = Assert.Single(problems);
        Assert.Equal(ActivationOutcome.Failed, problem.Outcome);
        Assert.Equal(new ActivationPlan(ActivationKind.NavigateFileDialog, @"D:\a", null), problem.Plan);
        Assert.Same(error, problem.Error);
        Assert.False(string.IsNullOrWhiteSpace(problem.Reason));
    }

    [Fact]
    public async Task A_failed_action_its_fallback_made_up_for_is_reported_with_the_final_outcome()
    {
        var error = new InvalidOperationException("tab did not appear");
        _explorer.OpenInNewTabException = error;
        var problems = new List<ActivationProblem>();

        var outcome = await CreateService(problems.Add).ActivateAsync(Folder(@"C:\Work"), ExplorerContext, ctrl: true);

        Assert.Equal(ActivationOutcome.OpenedElsewhere, outcome);
        var problem = Assert.Single(problems);
        Assert.Equal(ActivationOutcome.OpenedElsewhere, problem.Outcome);
        Assert.Equal(ActivationKind.OpenExplorerNewTab, problem.Plan?.Kind);
        Assert.Same(error, problem.Error);
    }

    [Fact]
    public async Task A_sink_that_throws_does_not_change_the_outcome()
    {
        _dialog.NavigateResult = false;

        var outcome = await CreateService(_ => throw new InvalidOperationException("log broken"))
            .ActivateAsync(Folder(@"D:\a"), DialogContext, ctrl: false);

        Assert.Equal(ActivationOutcome.Failed, outcome);
    }

    // Hand-written fakes.

    private readonly record struct ExplorerCall(string Method, nint Window, string Path);

    private sealed class FakeExplorerController : IExplorerController
    {
        public List<ExplorerCall> Calls { get; } = [];

        public bool NavigateResult { get; set; } = true;

        public bool OpenInNewTabResult { get; set; } = true;

        public Exception? NavigateException { get; set; }

        public Exception? OpenInNewTabException { get; set; }

        public Exception? OpenNewWindowException { get; set; }

        public bool Navigate(nint explorerWindow, string path)
        {
            Calls.Add(new ExplorerCall("Navigate", explorerWindow, path));
            return NavigateException is null ? NavigateResult : throw NavigateException;
        }

        public bool OpenInNewTab(nint explorerWindow, string path)
        {
            Calls.Add(new ExplorerCall("OpenInNewTab", explorerWindow, path));
            return OpenInNewTabException is null ? OpenInNewTabResult : throw OpenInNewTabException;
        }

        public void OpenNewWindow(string path)
        {
            Calls.Add(new ExplorerCall("OpenNewWindow", 0, path));
            if (OpenNewWindowException is not null)
            {
                throw OpenNewWindowException;
            }
        }
    }

    private sealed class FakeFileDialogNavigator : IFileDialogNavigator
    {
        public List<(nint Dialog, string Folder)> Calls { get; } = [];

        public bool NavigateResult { get; set; } = true;

        public Exception? NavigateException { get; set; }

        public bool Navigate(nint dialog, string folder)
        {
            Calls.Add((dialog, folder));
            return NavigateException is null ? NavigateResult : throw NavigateException;
        }
    }

    private sealed class FakeShellLauncher : IShellLauncher
    {
        public List<string> Calls { get; } = [];

        public Exception? OpenFileException { get; set; }

        /// <summary>What <see cref="OpenFile"/> returns: <c>false</c>, the shell told the user it could not.</summary>
        public bool OpenFileResult { get; set; } = true;

        public bool OpenFile(string path)
        {
            Calls.Add(path);
            if (OpenFileException is not null)
            {
                throw OpenFileException;
            }

            return OpenFileResult;
        }

        public Exception? OpenUrlException { get; set; }

        /// <summary>What <see cref="OpenUrl"/> returns.</summary>
        public bool OpenUrlResult { get; set; } = true;

        public bool OpenUrl(string url)
        {
            Calls.Add(url);
            if (OpenUrlException is not null)
            {
                throw OpenUrlException;
            }

            return OpenUrlResult;
        }
    }

    private sealed class FakePathResolver : IPathResolver
    {
        public List<(string Path, TimeSpan Timeout)> ExistsCalls { get; } = [];

        /// <summary>By default the item's own path.</summary>
        public Func<Item, string?> ResolveFunc { get; set; } = item => item.Path;

        /// <summary>By default everything exists.</summary>
        public Func<string, TimeSpan, Task<bool>> ExistsFunc { get; set; } = (_, _) => Task.FromResult(true);

        public string? Resolve(Item item) => ResolveFunc(item);

        public Task<bool> ExistsAsync(string path, TimeSpan timeout)
        {
            lock (ExistsCalls)
            {
                ExistsCalls.Add((path, timeout));
            }

            return ExistsFunc(path, timeout);
        }
    }
}
