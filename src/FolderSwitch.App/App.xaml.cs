// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Startup;
using FolderSwitch.Core.Localization;

namespace FolderSwitch.App;

/// <summary>
/// The resident application: composes everything at startup (<see cref="Composition"/>, after the first-run wizard on a
/// first start; closing the wizard ends the application), renders the main window once off-screen, then waits in the
/// background for the shortcut, the notification icon or a second launch, until « Quit ».
/// </summary>
/// <remarks>
/// <para>
/// An ordinary exception that reaches the dispatcher is logged and handled: a resident utility keeps running. A
/// critical one (<see cref="ExceptionPolicy.IsCritical"/>) is logged and let through: it ends the process, which
/// would otherwise stay as a zombie holding the single-instance mutex and the keyboard hook (an abandoned mutex is
/// taken over by the next start). So does a burst (<see cref="ErrorBreaker"/>): <see cref="ErrorBreaker.DefaultLimit"/>
/// handled exceptions within <see cref="ErrorBreaker.DefaultWindow"/> (one thrown on every layout pass, say) make
/// FolderSwitch quit with code 1.
/// </para>
/// <para>Explicit shutdown only (see <c>App.xaml</c>): hiding the only window does not end the application.</para>
/// </remarks>
public partial class App : Application
{
    /// <summary>A self-test that is not ready by then fails (exit code 2) instead of hanging.</summary>
    private static readonly TimeSpan SelfTestTimeout = TimeSpan.FromSeconds(5);

    private const int ExitOk = 0;
    private const int ExitFailed = 1;
    private const int ExitSelfTestTimeout = 2;
    private const int ExitSelfTestErrors = 3;

    private readonly StartupOptions _options;
    private readonly ErrorBreaker _breaker = new(TimeProvider.System);
    private Composition? _composition;
    private int _dispatcherErrors;
    private bool _quitting;

    internal App(StartupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Quit is posted: it is asked from the notification icon's own menu, which must be left before it goes away.
        var composition = new Composition(_options, () => Dispatcher.BeginInvoke(() => Quit(ExitOk)));
        _composition = composition;
        bool built;
        try
        {
            built = composition.Build();
        }
        catch (Exception exception)
        {
            DebugLog.Error("FolderSwitch could not start", exception);
            if (!_options.SelfTest)
            {
                ShowStartupFailure(composition.Localizer);
            }

            Quit(ExitFailed);
            return;
        }

        if (!built)
        {
            // The first-run wizard was closed without finishing: nothing was saved, the next start shows it again.
            DebugLog.Info("FolderSwitch quits: the first run was not completed");
            Quit(ExitOk);
            return;
        }

        DebugLog.Info(FormattableString.Invariant($"Composed {_options.Clock.Elapsed.TotalMilliseconds:F0} ms after Main started"));

        if (_options.SelfTest)
        {
            StartSelfTestWatchdog();
        }

        composition.Controller.Prerender(OnReady);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Also reached without Quit (the Windows session ends): released once either way.
        _composition?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// After a critical error escaped <see cref="Application.Run()"/>: releases what can still be released (keyboard hook,
    /// notification icon). Best effort, never throws.
    /// </summary>
    internal void ReleaseAfterFailure()
    {
        try
        {
            _composition?.Dispose();
        }
        catch (Exception e)
        {
            DebugLog.Error("Releasing after a critical error failed", e);
        }
    }

    /// <summary>Releases everything, then ends the application with <paramref name="exitCode"/>. Once.</summary>
    private void Quit(int exitCode)
    {
        if (_quitting)
        {
            return;
        }

        _quitting = true;
        _composition?.Dispose();
        Shutdown(exitCode);
    }

    /// <summary>The main window was rendered once and hidden again: FolderSwitch is ready.</summary>
    private void OnReady()
    {
        var sinceMain = _options.Clock.Elapsed.TotalMilliseconds;
        var sinceProcess = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        DebugLog.Info(FormattableString.Invariant(
            $"Startup: ready, main window rendered, {sinceMain:F0} ms after Main started ({sinceProcess:F0} ms after the process started)"));

        if (_options.SelfTest)
        {
            var passed = _dispatcherErrors == 0;
            DebugLog.Info(passed ? "Self-test passed" : $"Self-test failed: {_dispatcherErrors} error(s) reached the dispatcher");
            Quit(passed ? ExitOk : ExitSelfTestErrors);
            return;
        }

        _composition?.AnnounceReady();

        // After the startup, not inside it: each message waits for the previous one.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, ShowStartupNotices);
    }

    private void ShowStartupNotices()
    {
        if (_quitting || _composition?.Prompts is not { } prompts)
        {
            return;
        }

        _composition.ShowSetupNotice();
        foreach (var (key, isError) in _composition.StartupNotices)
        {
            if (isError)
            {
                prompts.ShowError(key);
            }
            else
            {
                prompts.ShowInfo(key);
            }
        }
    }

    private void StartSelfTestWatchdog()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = SelfTestTimeout };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            DebugLog.Error(FormattableString.Invariant($"Self-test failed: not ready after {SelfTestTimeout.TotalSeconds:F0} s"));
            Quit(ExitSelfTestTimeout);
        };
        timer.Start();
    }

    private static void ShowStartupFailure(Localizer? localizer)
    {
        // The localizer may be what failed: then the message is in English.
        var text = localizer is { Available.Count: > 0 } ? localizer["Startup.Failed"] : "FolderSwitch could not start.";
        MessageBox.Show(text + Environment.NewLine + Environment.NewLine + DebugLog.FilePath, "FolderSwitch",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _dispatcherErrors++;
        if (ExceptionPolicy.IsCritical(e.Exception))
        {
            // Not handled: it leaves Application.Run and ends the process (see Program.Main).
            DebugLog.Error("Critical exception on the UI thread: FolderSwitch stops", e.Exception);
            return;
        }

        DebugLog.Error("Unhandled exception on the UI thread (handled, FolderSwitch keeps running)", e.Exception);
        e.Handled = true;

        if (_quitting)
        {
            return;
        }

        if (_breaker.Record())
        {
            DebugLog.Error(FormattableString.Invariant(
                $"{_breaker.Count} exceptions on the UI thread within {_breaker.Window.TotalSeconds:F0} s: FolderSwitch stops"));
            Quit(ExitFailed);
        }
    }
}
