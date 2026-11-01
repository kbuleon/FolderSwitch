using FolderSwitch.Core.Diagnostics;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Activation;

/// <summary>
/// Carries out the click (or Ctrl+click) on an item, or « Open in File Explorer » from its menu: resolves its target,
/// checks that the target is reachable (never for a virtual <c>shell:</c> / <c>::{CLSID}</c> location), then runs the
/// action chosen by <see cref="ActivationRouter"/>.
/// </summary>
/// <remarks>
/// <para>
/// A web site (<see cref="ItemKind.Url"/>) skips the resolution and the check: its address is normalized and handed to the
/// default browser, and « Open in File Explorer » is <see cref="ActivationOutcome.TargetUnavailable"/> for it.
/// </para>
/// <para>
/// The existence check waits at most 2 seconds, even if the resolver ignores its timeout. Only that
/// check runs off the caller's thread; the actions run on the caller's synchronization context, so the
/// controllers are called from the thread that called <see cref="ActivateAsync"/>.
/// </para>
/// <para>
/// A failure of an action is reported as <see cref="ActivationOutcome.Failed"/>: nothing is thrown to the caller,
/// except a critical exception (<see cref="CriticalExceptions"/>: out of memory, say), which is let through so that the
/// process ends rather than goes on broken. A file the shell could not open, and said so itself, is
/// <see cref="ActivationOutcome.ReportedByShell"/>. When the plan has a fallback (a new window, for a navigation or a new
/// tab of the calling Explorer window), the primary action returning <c>false</c> or throwing triggers the fallback; only
/// if the fallback also fails is the outcome <see cref="ActivationOutcome.Failed"/>. Success is
/// <see cref="ActivationOutcome.Done"/> when the action took place in the calling window, and
/// <see cref="ActivationOutcome.OpenedElsewhere"/> when it opened a new window or an application.
/// </para>
/// <para>
/// Every outcome but a plain success is described to <c>onProblem</c>, for the log: the plan, the target and the
/// exception, if any. Whatever that sink does, the outcome stays the same.
/// </para>
/// </remarks>
public sealed class ActivationService(
    IExplorerController explorer,
    IFileDialogNavigator fileDialog,
    IShellLauncher shell,
    IPathResolver resolver,
    WindowsGeneration os,
    Action<ActivationProblem>? onProblem = null) : IItemActivator
{
    private static readonly TimeSpan ExistenceTimeout = TimeSpan.FromSeconds(2);

    /// <summary>What one action gave.</summary>
    private enum Step
    {
        Done,
        Failed,
        ReportedByShell,
    }

    public Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, bool ctrl) =>
        item.Kind == ItemKind.Url
            ? Task.FromResult(OpenUrl(item, ctx, ctrl))
            : ActivateAsync(item, ctx, resolved => ActivationRouter.Plan(ctx, item.Kind, resolved, ctrl, os));

    public Task<ActivationOutcome> OpenInExplorerAsync(Item item) =>
        item.Kind == ItemKind.Url
            ? Task.FromResult(Report(ActivationOutcome.TargetUnavailable, "a web address has no folder to show", item.Url, null, null))
            : ActivateAsync(item, CallContext.None, resolved => ActivationRouter.PlanInExplorer(item.Kind, resolved));

    public bool CtrlOpensNewTab(ItemKind kind, CallContext ctx) => ActivationRouter.CtrlOpensNewTab(ctx, kind, os);

    /// <summary>Resolves <paramref name="item"/>, checks its target, then runs the plan <paramref name="planFor"/> makes for it.</summary>
    private async Task<ActivationOutcome> ActivateAsync(Item item, CallContext ctx, Func<string, ActivationPlan> planFor)
    {
        string? resolved = null;
        ActivationPlan? plan = null;
        try
        {
            resolved = resolver.Resolve(item);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return Report(ActivationOutcome.TargetUnavailable, "the item resolves to no location", null, null, null);
            }

            // For a file in a file dialog this checks the file itself, not only its folder.
            if (!ShellForms.IsShellForm(resolved) && !await ExistsWithinTimeoutAsync(resolved))
            {
                return Report(
                    ActivationOutcome.TargetUnavailable,
                    "the target does not exist, or its check did not answer within 2 s",
                    resolved,
                    null,
                    null);
            }

            plan = planFor(resolved);
            return Run(plan, ctx);
        }
        catch (Exception e) when (!CriticalExceptions.IsCritical(e))
        {
            return Report(ActivationOutcome.Failed, "an exception was thrown", resolved, plan, e);
        }
    }

    /// <summary>
    /// Opens a web site: there is nothing to resolve nor to check on disk. The stored address may have been edited by
    /// hand (spaces, upper case), so it goes through <see cref="WebAddress.TryNormalize"/> first; one that is not an
    /// <c>http</c> or <c>https</c> address makes the item unavailable, and nothing is launched.
    /// </summary>
    private ActivationOutcome OpenUrl(Item item, CallContext ctx, bool ctrl)
    {
        ActivationPlan? plan = null;
        try
        {
            if (!WebAddress.TryNormalize(item.Url, out var url))
            {
                return Report(ActivationOutcome.TargetUnavailable, "the item holds no valid http or https address", item.Url, null, null);
            }

            plan = ActivationRouter.Plan(ctx, item.Kind, url, ctrl, os);
            return Run(plan, ctx);
        }
        catch (Exception e) when (!CriticalExceptions.IsCritical(e))
        {
            return Report(ActivationOutcome.Failed, "an exception was thrown", item.Url, plan, e);
        }
    }

    /// <summary>Where the action of <paramref name="kind"/> takes place when it succeeds.</summary>
    private static ActivationOutcome SuccessOf(ActivationKind kind) =>
        kind is ActivationKind.OpenNewExplorerWindow or ActivationKind.OpenFile or ActivationKind.OpenUrl
            ? ActivationOutcome.OpenedElsewhere
            : ActivationOutcome.Done;

    private async Task<bool> ExistsWithinTimeoutAsync(string path)
    {
        // Task.Run: ExistsAsync starts on the thread pool, so an implementation that blocks its
        // calling thread, or that would resume on a captured synchronization context, cannot hold
        // up the caller.
        var check = Task.Run(() => resolver.ExistsAsync(path, ExistenceTimeout));

        using var deadlineSource = new CancellationTokenSource();
        var deadline = Task.Delay(ExistenceTimeout, deadlineSource.Token);

        if (await Task.WhenAny(check, deadline) != check)
        {
            // Timed out: a late result is ignored, a late failure is observed.
            _ = check.ContinueWith(
                static late => { _ = late.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return false;
        }

        deadlineSource.Cancel();

        try
        {
            return await check;
        }
        catch (Exception e) when (!CriticalExceptions.IsCritical(e))
        {
            // The check itself failed: the target cannot be confirmed reachable.
            return false;
        }
    }

    private ActivationOutcome Run(ActivationPlan plan, CallContext ctx)
    {
        if (plan.Fallback is not { } fallback)
        {
            // An exception here reaches the catch of ActivateAsync, and ends up as Failed.
            return Execute(plan.Kind, plan.Target, ctx) switch
            {
                Step.Done => SuccessOf(plan.Kind),
                Step.ReportedByShell => Report(
                    ActivationOutcome.ReportedByShell,
                    "the shell could not open the file and showed its own message",
                    plan.Target,
                    plan,
                    null),
                _ => Report(ActivationOutcome.Failed, "the action reported a failure", plan.Target, plan, null),
            };
        }

        // With a fallback, the primary action throwing counts as failing, exactly like returning
        // false: opening a tab drives Explorer through undocumented means that tend to throw.
        Step primary;
        Exception? primaryError = null;
        try
        {
            primary = Execute(plan.Kind, plan.Target, ctx);
        }
        catch (Exception e) when (!CriticalExceptions.IsCritical(e))
        {
            primary = Step.Failed;
            primaryError = e;
        }

        if (primary == Step.Done)
        {
            return SuccessOf(plan.Kind);
        }

        Step second;
        try
        {
            second = Execute(fallback, plan.Target, ctx);
        }
        catch (Exception e) when (!CriticalExceptions.IsCritical(e))
        {
            var error = primaryError is null ? e : new AggregateException(primaryError, e);
            return Report(ActivationOutcome.Failed, "the action failed, then its fallback threw", plan.Target, plan, error);
        }

        return second == Step.Done
            ? Report(SuccessOf(fallback), "the action failed; its fallback was carried out", plan.Target, plan, primaryError)
            : Report(ActivationOutcome.Failed, "the action and its fallback failed", plan.Target, plan, primaryError);
    }

    private Step Execute(ActivationKind kind, string target, CallContext ctx)
    {
        switch (kind)
        {
            case ActivationKind.NavigateExplorer:
                return explorer.Navigate(ctx.Window, target) ? Step.Done : Step.Failed;
            case ActivationKind.OpenExplorerNewTab:
                return explorer.OpenInNewTab(ctx.Window, target) ? Step.Done : Step.Failed;
            case ActivationKind.OpenNewExplorerWindow:
                explorer.OpenNewWindow(target);
                return Step.Done;
            case ActivationKind.NavigateFileDialog:
                return fileDialog.Navigate(ctx.Window, target) ? Step.Done : Step.Failed;
            case ActivationKind.OpenFile:
                return shell.OpenFile(target) ? Step.Done : Step.ReportedByShell;
            case ActivationKind.OpenUrl:
                return shell.OpenUrl(target) ? Step.Done : Step.ReportedByShell;
            default:
                return Step.Failed;
        }
    }

    /// <summary>Describes the problem to the sink, if any, and returns <paramref name="outcome"/> whatever the sink does.</summary>
    private ActivationOutcome Report(ActivationOutcome outcome, string reason, string? target, ActivationPlan? plan, Exception? error)
    {
        if (onProblem is { } sink)
        {
            try
            {
                sink(new ActivationProblem(outcome, reason, target, plan, error));
            }
            catch (Exception e) when (!CriticalExceptions.IsCritical(e))
            {
                // The log must not change what the user is told.
            }
        }

        return outcome;
    }
}
