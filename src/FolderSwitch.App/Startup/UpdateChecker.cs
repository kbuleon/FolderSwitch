// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Services;
using FolderSwitch.Core.Updates;
using Velopack;

namespace FolderSwitch.App.Startup;

/// <summary>What the last check found, for the notification area.</summary>
/// <param name="Version">The version that can be installed, as shown to the user; <c>null</c> when FolderSwitch is up to date.</param>
/// <param name="Notify">
/// Whether the notification area should announce it with a balloon: the user was not told about this version or a newer
/// one yet, and the check was not one they asked for.
/// </param>
internal sealed record UpdateAvailability(string? Version, bool Notify);

/// <summary>
/// Looks for a new version in the GitHub releases of the repository (<see cref="UpdateFeed.Url"/>) with Velopack, and
/// installs it when the user accepts: about a minute after the start, then every 24 hours (while the setting
/// « check for updates automatically » is on, see <see cref="SetAutomatic"/>), or when the user asks in the settings.
/// </summary>
/// <remarks>
/// <para>
/// There is no checker (<see cref="TryCreate"/> returns <c>null</c>) in a self-test, without a feed address, with an
/// address that is not a GitHub repository (<see cref="UpdateFeed.CreateSource"/>), and in a copy that Velopack did
/// not install (a development run): the update calls would fail there.
/// </para>
/// <para>
/// What a check finds is published (<see cref="UpdateAvailabilityChanged"/>): the menu entry of the notification area
/// follows the last check, and a balloon announces a version once per session, then again only when a newer one comes
/// out (<see cref="UpdateAnnouncements{TVersion}"/>). The entry opens the offer (<see cref="OfferAnnouncedAsync"/>).
/// Failures of the periodic check are logged, never shown; the user is told about those of what they asked for (the
/// check of the settings, the installation).
/// </para>
/// <para>
/// To install, the package is downloaded (progress in the log, <see cref="DownloadingChanged"/> for the menu entry),
/// then Velopack's updater is asked to wait for FolderSwitch to exit, apply the update and restart it, and FolderSwitch
/// quits through its usual path: the keyboard hook and the notification icon are released properly, which an immediate
/// exit would not do.
/// </para>
/// <para>
/// UI thread only; one operation at a time (<see cref="IsBusy"/>): a request made meanwhile is ignored, and the
/// interface says so (the settings button is disabled, the menu entry shows the download).
/// </para>
/// </remarks>
internal sealed class UpdateChecker : IDisposable
{
    /// <summary>The first check comes this long after <see cref="SetAutomatic"/> turned the periodic check on: never on the startup path.</summary>
    internal static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);

    /// <summary>The time between two periodic checks.</summary>
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    /// <summary>A periodic check that finds an operation of the user running tries again after this.</summary>
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMinutes(5);

    /// <summary>A check that takes longer fails (the package download has its own, longer, limit in Velopack).</summary>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The download progress is logged every this many percent.</summary>
    private const int ProgressLogStep = 25;

    private readonly UpdateManager _manager;
    private readonly UserPrompts _prompts;
    private readonly Action _quit;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly UpdateAnnouncements<SemanticVersion> _announcements = new();
    private UpdateInfo? _found;
    private bool _busy;
    private bool _disposed;

    private UpdateChecker(UpdateManager manager, UserPrompts prompts, Action quit)
    {
        _manager = manager;
        _prompts = prompts;
        _quit = quit;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = FirstCheckDelay };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>A check (periodic or asked for) found an update, or found none any more; raised on the UI thread.</summary>
    public event EventHandler<UpdateAvailability>? UpdateAvailabilityChanged;

    /// <summary>The download of the update started (<c>true</c>) or failed (<c>false</c>); raised on the UI thread.</summary>
    public event EventHandler<bool>? DownloadingChanged;

    /// <summary>A check, an offer or a download started or ended (<see cref="IsBusy"/>); raised on the UI thread.</summary>
    public event EventHandler? BusyChanged;

    /// <summary>Whether an operation is running: the next requests are ignored until it ends.</summary>
    public bool IsBusy => _busy;

    /// <summary>
    /// The checker for this run, or <c>null</c> when updates are not available: self-test, no feed address, a copy
    /// that is not installed by Velopack, or an address that is not a GitHub repository (all logged).
    /// </summary>
    /// <param name="feedUrl">See <see cref="UpdateFeed.Url"/>.</param>
    /// <param name="selfTest">A self-test never looks for updates.</param>
    /// <param name="prompts">Where the user is asked and told.</param>
    /// <param name="quit">Ends the application, once an update is ready to be applied.</param>
    public static UpdateChecker? TryCreate(string feedUrl, bool selfTest, UserPrompts prompts, Action quit)
    {
        ArgumentNullException.ThrowIfNull(feedUrl);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(quit);

        if (selfTest)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            DebugLog.Info("Updates: no feed address, the check is disabled");
            return null;
        }

        var source = UpdateFeed.CreateSource(feedUrl);
        if (source is null)
        {
            DebugLog.Warn($"Updates: the feed address « {feedUrl} » is not a GitHub repository, the check is disabled");
            return null;
        }

        try
        {
            var manager = new UpdateManager(source);
            if (!manager.IsInstalled)
            {
                DebugLog.Info("Updates: this copy was not installed by Velopack (development run), the check is disabled");
                return null;
            }

            DebugLog.Info($"Updates: GitHub releases of {source.RepoUri.AbsolutePath.Trim('/')}");
            DebugLog.Info(FormattableString.Invariant(
                $"Updates: version {manager.CurrentVersion}, first check in {FirstCheckDelay.TotalMinutes:F0} min, then every {CheckInterval.TotalHours:F0} h"));
            return new UpdateChecker(manager, prompts, quit);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Warn("Updates: unavailable, the check is disabled", e);
            return null;
        }
    }

    /// <summary>
    /// Turns the periodic check on (the first one comes after <see cref="FirstCheckDelay"/>) or off, following the
    /// setting. Asking for the state it is in already does nothing; turning it off stops the timer only: a check or a
    /// download that is running ends normally, and the update already found stays offered.
    /// </summary>
    public void SetAutomatic(bool enabled)
    {
        if (_disposed || enabled == _timer.IsEnabled)
        {
            return;
        }

        if (enabled)
        {
            _timer.Interval = FirstCheckDelay;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        DebugLog.Info(enabled ? "Updates: automatic check on" : "Updates: automatic check off");
    }

    /// <summary>
    /// The check the user asked for (settings): says « up to date », offers the update found, or says that the check
    /// failed. Does nothing while another operation is running. Never throws.
    /// </summary>
    public async Task CheckNowAsync()
    {
        if (_busy || _disposed)
        {
            return;
        }

        SetBusy(true);
        try
        {
            UpdateInfo? update;
            try
            {
                update = await FindAsync();
            }
            catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
            {
                DebugLog.Warn("Updates: the check asked by the user failed", e);
                if (!_disposed)
                {
                    _prompts.ShowError("Update.CheckFailed");
                }

                return;
            }

            if (_disposed)
            {
                return;
            }

            Publish(update, notify: false);
            if (update is null)
            {
                _prompts.ShowInfo("Update.UpToDate");
                return;
            }

            // What this check returned, not what an earlier one found.
            await OfferAsync(update);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Error("Updates: the check asked by the user stopped on an error", e);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Offers the update found by the last check (menu entry or notification): « Install » downloads it and restarts
    /// FolderSwitch, « Later » leaves the entry in the menu. Does nothing while another operation is running. A
    /// critical exception is let through (see <see cref="ExceptionPolicy"/>); the others are logged.
    /// </summary>
    public async Task OfferAnnouncedAsync()
    {
        if (_busy || _disposed || _found is not { } update)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await OfferAsync(update);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Error("Updates: the offer stopped on an error", e);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Stops the periodic check and any download in progress. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_busy)
        {
            // An operation of the user's is running: try again soon, not in 24 hours.
            _timer.Interval = BusyRetryDelay;
            return;
        }

        // From now on every 24 hours (the first interval was the short one).
        _timer.Interval = CheckInterval;
        SetBusy(true);
        try
        {
            Publish(await FindAsync(), notify: true);
        }
        catch (Exception exception) when (!ExceptionPolicy.IsCritical(exception))
        {
            // Never shown: the next check is in 24 hours, and the user did not ask for this one.
            DebugLog.Warn("Updates: the periodic check failed", exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Asks the feed; <c>null</c> when FolderSwitch is up to date.</summary>
    private async Task<UpdateInfo?> FindAsync()
    {
        DebugLog.Info($"Updates: checking (installed version {_manager.CurrentVersion})");
        var update = await _manager.CheckForUpdatesAsync().WaitAsync(CheckTimeout, _cancellation.Token);
        DebugLog.Info(update is null ? "Updates: up to date" : $"Updates: version {update.TargetFullRelease.Version} is available");
        return update;
    }

    /// <summary>
    /// Keeps what the check found as the update to offer (none: up to date) and tells the notification area. The
    /// balloon is for a version that the user was not told about yet (<see cref="UpdateAnnouncements{TVersion}"/>).
    /// </summary>
    private void Publish(UpdateInfo? update, bool notify)
    {
        _found = update;
        if (update is null)
        {
            UpdateAvailabilityChanged?.Invoke(this, new UpdateAvailability(null, Notify: false));
            return;
        }

        var version = update.TargetFullRelease.Version;
        var isNew = _announcements.TryAnnounce(version);
        UpdateAvailabilityChanged?.Invoke(this, new UpdateAvailability(version.ToString(), notify && isNew));
    }

    private async Task OfferAsync(UpdateInfo update)
    {
        var version = update.TargetFullRelease.Version.ToString();
        if (!_prompts.OfferUpdate(version))
        {
            DebugLog.Info($"Updates: version {version} postponed by the user");
            return;
        }

        DownloadingChanged?.Invoke(this, true);
        try
        {
            DebugLog.Info($"Updates: downloading version {version}");

            // Called from pool threads: only logs.
            var lastLogged = 0;
            void OnProgress(int percent)
            {
                if (percent / ProgressLogStep > lastLogged / ProgressLogStep)
                {
                    lastLogged = percent;
                    DebugLog.Info($"Updates: download {percent} %");
                }
            }

            await _manager.DownloadUpdatesAsync(update, OnProgress, _cancellation.Token);
            DebugLog.Info($"Updates: version {version} downloaded, FolderSwitch quits so that it can be applied");

            // The updater waits for this process to exit, applies the update and starts FolderSwitch again.
            _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: false, restart: true);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Error($"Updates: version {version} could not be installed", e);
            DownloadingChanged?.Invoke(this, false);
            if (!_disposed)
            {
                _prompts.ShowError("Update.InstallFailed");
            }

            return;
        }

        _quit();
    }

    private void SetBusy(bool busy)
    {
        if (_busy == busy)
        {
            return;
        }

        _busy = busy;
        BusyChanged?.Invoke(this, EventArgs.Empty);
    }
}
