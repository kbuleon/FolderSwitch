// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// The Windows generation, the user's display language and the application theme (light or dark),
/// with a notification when that theme changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Theme.</b> <see cref="AppsUseDarkTheme"/> reads <c>AppsUseLightTheme</c> under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize</c> each time: 0 is dark,
/// anything else or no value is light.
/// </para>
/// <para>
/// <b>Change notification.</b> The registry is watched (<c>RegNotifyChangeKeyValue</c>) rather than
/// <c>WM_SETTINGCHANGE</c> listened for: Windows broadcasts <c>WM_SETTINGCHANGE</c>
/// (<c>ImmersiveColorSet</c>) after writing that value, so watching the value itself sees the same
/// changes, and the event can never disagree with <see cref="AppsUseDarkTheme"/>. It needs no window:
/// a broadcast reaches top-level windows only (not message-only ones), so it would take a hidden
/// top-level window and a message loop thread of its own; here a registered wait on the thread pool
/// is enough. It can also be tested on a key of its own without touching the user's theme. The
/// parent key (<c>Themes</c>) is watched with its subkeys, so that a <c>Personalize</c> key created
/// later (a profile that never changed its colors) is seen too. Any change there is a notification;
/// <see cref="ThemeChanged"/> is raised only when the value read differs from the last one.
/// </para>
/// <para>
/// <b>Threads.</b> <see cref="ThemeChanged"/> is posted to the <see cref="SynchronizationContext"/>
/// given to the constructor, or raised on a thread-pool thread without one. Not raised after
/// <see cref="Dispose"/>, including a notification already posted.
/// </para>
/// </remarks>
public sealed class SystemInfo : ISystemInfo, IDisposable
{
    /// <summary>The key, under <c>HKEY_CURRENT_USER</c>, that holds the application theme.</summary>
    internal const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private readonly SynchronizationContext? _context;
    private readonly string _personalizeKeyPath;

    // ---- Guarded by _gate ----
    private readonly Lock _gate = new();
    private RegistryKey? _watchedKey;
    private AutoResetEvent? _changed;
    private RegisteredWaitHandle? _wait;
    private bool _lastDark;

    // ---- Interlocked ----
    private int _disposed; // set under _gate too
    private int _notifications;

    /// <param name="context">Where <see cref="ThemeChanged"/> is posted (the UI thread's context).</param>
    public SystemInfo(SynchronizationContext? context)
        : this(context, PersonalizeKeyPath)
    {
    }

    /// <summary>Test seam: the theme read from, and watched under, another key of <c>HKEY_CURRENT_USER</c>.</summary>
    internal SystemInfo(SynchronizationContext? context, string personalizeKeyPath)
    {
        _context = context;
        _personalizeKeyPath = personalizeKeyPath;
        StartWatching();
    }

    /// <inheritdoc/>
    /// <remarks>Raised on the constructor's context, or on a thread-pool thread without one.</remarks>
    public event EventHandler? ThemeChanged;

    /// <inheritdoc/>
    public WindowsGeneration Generation => WindowsVersion.IsWindows11 ? WindowsGeneration.Windows11 : WindowsGeneration.Windows10;

    /// <inheritdoc/>
    /// <remarks>
    /// The first of the user's preferred display languages (<c>GetUserPreferredUILanguages</c>), the
    /// one Windows shows its interface in: not <see cref="CultureInfo.CurrentUICulture"/>, which the
    /// application may change to its own language. Falls back to it if the call fails.
    /// </remarks>
    public string UiCultureName => PreferredUiLanguage() ?? CultureInfo.CurrentUICulture.Name;

    /// <inheritdoc/>
    public bool AppsUseDarkTheme => ReadDark(_personalizeKeyPath);

    /// <summary>Test seam: whether the key is watched (it exists and the watch could be set).</summary>
    internal bool IsWatching
    {
        get
        {
            lock (_gate)
            {
                return _wait is not null;
            }
        }
    }

    /// <summary>
    /// Test seam: how many change notifications have been handled, the event raised (without a context)
    /// or posted (with one) or not.
    /// </summary>
    internal int NotificationCount => Volatile.Read(ref _notifications);

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Stops watching the registry. Idempotent.</summary>
    public void Dispose()
    {
        RegisteredWaitHandle? wait;
        RegistryKey? key;
        AutoResetEvent? changed;
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            (wait, key, changed) = (_wait, _watchedKey, _changed);
            (_wait, _watchedKey, _changed) = (null, null, null);
        }

        // Not waited for: Dispose may be called from a ThemeChanged handler run by that wait. A
        // notification that comes in meanwhile finds the instance disposed. Closing the key cancels
        // its pending notification; the wait keeps its own reference to the event's handle.
        wait?.Unregister(null);
        key?.Dispose();
        changed?.Dispose();
    }

    private static bool ReadDark(string personalizeKeyPath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(personalizeKeyPath);
            return key?.GetValue(AppsUseLightThemeValue) switch
            {
                int value => value == 0,
                long value => value == 0,
                _ => false,
            };
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static unsafe string? PreferredUiLanguage()
    {
        try
        {
            uint count;
            uint length = 0;
            if (!PInvoke.GetUserPreferredUILanguages(PInvoke.MUI_LANGUAGE_NAME, &count, default, &length) || length == 0)
            {
                return null;
            }

            // A double-null-terminated list of names: the first one is wanted.
            var buffer = new char[length];
            fixed (char* names = buffer)
            {
                if (!PInvoke.GetUserPreferredUILanguages(PInvoke.MUI_LANGUAGE_NAME, &count, names, &length))
                {
                    return null;
                }
            }

            var end = Array.IndexOf(buffer, '\0');
            return end > 0 ? new string(buffer, 0, end) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void StartWatching()
    {
        var separator = _personalizeKeyPath.LastIndexOf('\\');
        var watchedPath = separator > 0 ? _personalizeKeyPath[..separator] : _personalizeKeyPath;
        lock (_gate)
        {
            try
            {
                // Read access includes KEY_NOTIFY.
                _watchedKey = Registry.CurrentUser.OpenSubKey(watchedPath);
                if (_watchedKey is not null)
                {
                    _changed = new AutoResetEvent(false);
                    if (Arm())
                    {
                        _wait = ThreadPool.RegisterWaitForSingleObject(
                            _changed,
                            static (state, _) => ((SystemInfo)state!).OnChanged(),
                            this,
                            Timeout.Infinite,
                            executeOnlyOnce: false);
                    }
                }
            }
            catch (Exception)
            {
                // Not watched: the theme is still read, ThemeChanged is never raised.
            }

            if (_wait is null)
            {
                _watchedKey?.Dispose();
                _changed?.Dispose();
                (_watchedKey, _changed) = (null, null);
            }

            // Read once armed, so that a change made from now on is notified.
            _lastDark = ReadDark(_personalizeKeyPath);
        }
    }

    /// <summary>Under the gate: asks for the next change notification (each one is for a single change).</summary>
    private bool Arm()
    {
        // Thread-agnostic: the registration does not end with the thread that made it, a thread-pool
        // thread when re-armed from OnChanged.
        var result = PInvoke.RegNotifyChangeKeyValue(
            _watchedKey!.Handle,
            bWatchSubtree: true,
            REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC,
            _changed!.SafeWaitHandle,
            fAsynchronous: true);
        return result == WIN32_ERROR.ERROR_SUCCESS;
    }

    /// <summary>On a thread-pool thread, each time the watched key or one of its subkeys changes.</summary>
    private void OnChanged()
    {
        bool raise;
        lock (_gate)
        {
            if (IsDisposed || _wait is null)
            {
                return;
            }

            // Re-armed before reading: a change made after the read is notified again.
            bool armed;
            try
            {
                armed = Arm();
            }
            catch (Exception)
            {
                armed = false;
            }

            if (!armed)
            {
                // The key is gone: no more notifications will come.
                _wait.Unregister(null);
                _wait = null;
            }

            var dark = ReadDark(_personalizeKeyPath);
            raise = dark != _lastDark;
            _lastDark = dark;
        }

        if (raise)
        {
            Raise();
        }

        // Counted once handled: raised (without a context) or posted.
        Interlocked.Increment(ref _notifications);
    }

    private void Raise()
    {
        if (_context is null)
        {
            RaiseNow();
        }
        else
        {
            _context.Post(static state => ((SystemInfo)state!).RaiseNow(), this);
        }
    }

    private void RaiseNow()
    {
        if (!IsDisposed)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
