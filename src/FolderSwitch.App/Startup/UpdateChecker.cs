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
internal sealed record UpdateAvailability(string? Version);

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
/// follows the last check, at once. When the periodic check finds a version whose box did not open yet in this session
/// (<see cref="UpdateAnnouncements{TVersion}"/>), the « Install / Later » box opens by itself, but only while FolderSwitch
/// is not in use: no window, box or picker of it on screen (the <c>isInUse</c> test given to <see cref="TryCreate"/>).
/// Otherwise the offer is postponed: it is tried again every <see cref="PostponeRetryDelay"/>, with the update already
/// found (GitHub is not asked again), until FolderSwitch is left alone. That box has no owner: it opens on top of the
/// other windows without taking the keyboard focus (see <see cref="UserPrompts.OfferUpdate"/>), which the shortcut gives
/// it. A box opens once per version: « Later » keeps the entry in the menu, and only a newer version opens a box again;
/// the box opened from the entry (<see cref="OfferAnnouncedAsync"/>) or by the check of the settings counts too, and
/// drops a postponed offer of that version. Failures of the periodic check are logged, never shown; the user is told
/// about those of what they asked for (the check of the settings) and of the installation, whoever started it.
/// </para>
/// <para>
/// To install, the package is downloaded (progress in the log, <see cref="DownloadingChanged"/> for the menu entry),
/// then Velopack's updater is asked to wait for FolderSwitch to exit, apply the update and restart it, and FolderSwitch
/// quits through its usual path: the keyboard hook and the notification icon are released properly, which an immediate
/// exit would not do.
/// </para>
/// <para>
/// UI thread only; one operation at a time (<see cref="IsBusy"/>: a check, an open box, a download; not a postponed
/// offer, which waits without holding anything): a request made meanwhile is ignored, and the interface says so (the
/// settings button is disabled, the menu entry shows the download).
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

    /// <summary>A postponed offer (FolderSwitch was in use) is tried again this often, until its box can open.</summary>
    internal static readonly TimeSpan PostponeRetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>A check that takes longer fails (the package download has its own, longer, limit in Velopack).</summary>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The download progress is logged every this many percent.</summary>
    private const int ProgressLogStep = 25;

    private readonly UpdateManager _manager;
    private readonly UserPrompts _prompts;
    private readonly Action _quit;
    private readonly Func<bool> _isInUse;
    private readonly DispatcherTimer _timer;

    /// <summary>Runs while an offer is postponed (<see cref="UpdateAnnouncements{TVersion}.Pending"/>).</summary>
    private readonly DispatcherTimer _retry;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly UpdateAnnouncements<SemanticVersion> _announcements = new();
    private UpdateInfo? _found;
    private bool _busy;
    private bool _disposed;

    private UpdateChecker(UpdateManager manager, UserPrompts prompts, Action quit, Func<bool> isInUse)
    {
        _manager = manager;
        _prompts = prompts;
        _quit = quit;
        _isInUse = isInUse;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = FirstCheckDelay };
        _timer.Tick += OnTimerTick;
        _retry = new DispatcherTimer(DispatcherPriority.Background) { Interval = PostponeRetryDelay };
        _retry.Tick += OnRetryTick;
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
    /// <param name="isInUse">
    /// Whether FolderSwitch is in use (a window, a box, a picker or the notification menu of it on screen): the box of
    /// the periodic check waits meanwhile.
    /// </param>
    public static UpdateChecker? TryCreate(string feedUrl, bool selfTest, UserPrompts prompts, Action quit, Func<bool> isInUse)
    {
        ArgumentNullException.ThrowIfNull(feedUrl);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(quit);
        ArgumentNullException.ThrowIfNull(isInUse);

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
            return new UpdateChecker(manager, prompts, quit, isInUse);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Warn("Updates: unavailable, the check is disabled", e);
            return null;
        }
    }

    /// <summary>
    /// Turns the periodic check on (the first one comes after <see cref="FirstCheckDelay"/>) or off, following the
    /// setting. Asking for the state it is in already does nothing; turning it off stops the timers only: a check or a
    /// download that is running ends normally, and the update already found stays offered in the menu, but a postponed
    /// box no longer opens by itself.
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
            _retry.Stop();
            _announcements.Drop();
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

            Publish(update, periodic: false);
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
    /// Offers the update found by the last check (the menu entry): « Install » downloads it and restarts FolderSwitch,
    /// « Later » leaves the entry in the menu; either way a postponed box of that version no longer opens by itself.
    /// Does nothing while another operation is running. A critical exception is let through (see
    /// <see cref="ExceptionPolicy"/>); the others are logged.
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

    /// <summary>Stops the periodic check, a postponed offer and any download in progress. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _retry.Stop();
        _retry.Tick -= OnRetryTick;
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
        var waiting = false;
        SetBusy(true);
        try
        {
            var update = await FindAsync();
            if (_disposed)
            {
                return;
            }

            waiting = Publish(update, periodic: true);
        }
        catch (Exception exception) when (!ExceptionPolicy.IsCritical(exception))
        {
            // Never shown: the next check is in 24 hours, and the user did not ask for this one (the failure of an
            // installation is shown by OfferAsync).
            DebugLog.Warn("Updates: the periodic check failed", exception);
        }
        finally
        {
            SetBusy(false);
        }

        // Out of the check's busy state: a postponed offer holds nothing while it waits.
        if (waiting)
        {
            await OfferPendingAsync();
        }
    }

    /// <summary>A postponed offer tries again: its box opens if FolderSwitch is no longer in use.</summary>
    private async void OnRetryTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        await OfferPendingAsync();
    }

    /// <summary>
    /// Opens the box of the version the periodic check found (<see cref="UpdateAnnouncements{TVersion}.Pending"/>) if
    /// FolderSwitch is not in use and no other operation runs; otherwise postpones it (<see cref="_retry"/>). Nothing
    /// waits any more once the box opened, whoever opened it. A critical exception is let through (see
    /// <see cref="ExceptionPolicy"/>); the others are logged.
    /// </summary>
    private async Task OfferPendingAsync()
    {
        if (_disposed)
        {
            return;
        }

        switch (_announcements.Decide(_busy, _isInUse()))
        {
            case AutomaticOffer.None:
                _retry.Stop();
                return;
            case AutomaticOffer.Postpone:
                if (!_retry.IsEnabled)
                {
                    DebugLog.Info($"Updates: FolderSwitch is in use, the offer of version {_announcements.Pending} waits");
                    _retry.Start();
                }

                return;
        }

        _retry.Stop();

        // The update found must still be the one that waits (a later check may have found another, or none).
        if (_found is not { } update || _announcements.Pending is not { } pending
            || update.TargetFullRelease.Version.CompareTo(pending) != 0)
        {
            _announcements.Drop();
            return;
        }

        SetBusy(true);
        try
        {
            await OfferAsync(update, automatic: true);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Error("Updates: the automatic offer stopped on an error", e);
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
    /// Keeps what the check found as the update to offer (none: up to date, and no offer waits any more) and tells the
    /// notification area at once.
    /// </summary>
    /// <param name="periodic">
    /// The periodic check found it: a version whose box did not open yet in this session waits for its box
    /// (<see cref="UpdateAnnouncements{TVersion}.Hold"/>). The check of the settings offers what it found itself.
    /// </param>
    /// <returns>Whether a box waits for the version found (<see cref="OfferPendingAsync"/>).</returns>
    private bool Publish(UpdateInfo? update, bool periodic)
    {
        _found = update;
        if (update is null)
        {
            _announcements.Drop();
            UpdateAvailabilityChanged?.Invoke(this, new UpdateAvailability(null));
            return false;
        }

        var version = update.TargetFullRelease.Version;
        var waiting = periodic && _announcements.Hold(version);
        UpdateAvailabilityChanged?.Invoke(this, new UpdateAvailability(version.ToString()));
        return waiting;
    }

    /// <summary>
    /// Opens the « Install / Later » box of <paramref name="update"/>, which counts as its announcement
    /// (<see cref="UpdateAnnouncements{TVersion}.TryAnnounce"/>), and installs it on « Install ».
    /// </summary>
    /// <param name="update">The update to offer.</param>
    /// <param name="automatic">The periodic check made the offer, not the user: the box is shown on top, without an owner (see <see cref="UserPrompts.OfferUpdate"/>).</param>
    private async Task OfferAsync(UpdateInfo update, bool automatic = false)
    {
        var version = update.TargetFullRelease.Version.ToString();
        _announcements.TryAnnounce(update.TargetFullRelease.Version);
        if (!_prompts.OfferUpdate(version, automatic))
        {
            DebugLog.Info($"Updates: version {version} postponed by the user");
            return;
        }

        // Released while the box was open (the Windows session ending): no download for a composition that is gone.
        if (_disposed)
        {
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
            if (!_disposed)
            {
                DownloadingChanged?.Invoke(this, false);
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
