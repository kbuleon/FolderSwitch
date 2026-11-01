using System.Windows;
using CommunityToolkit.Mvvm.Input;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Core.Localization;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using DrawingIcon = System.Drawing.Icon;

namespace FolderSwitch.App.Tray;

/// <summary>
/// The notification area icon. It only reports what the user asked for (<see cref="OpenRequested"/> on a left click
/// as for the « Open » entry, then one event per other menu entry); the menu texts follow the language of the
/// interface. It also announces an available update (<see cref="AnnounceUpdate"/>) and shows its download
/// (<see cref="SetUpdateDownloading"/>), and, after the first run, an installation or an update, that FolderSwitch is
/// ready (<see cref="ShowReady"/>).
/// </summary>
/// <remarks>UI thread only. <see cref="Close"/> removes the icon.</remarks>
public partial class TrayIcon : TaskbarIcon
{
    private readonly Localizer _localizer;
    private readonly DrawingIcon _image;
    private string? _updateVersion;
    private bool _downloading;
    private bool _closed;

    /// <param name="show">
    /// Whether the icon is put in the notification area; <c>false</c> (a self-test) builds everything but never shows it.
    /// </param>
    public TrayIcon(Localizer localizer, bool show)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        InitializeComponent();
        _localizer = localizer;
        _image = TrayIconImage.Create();
        Icon = _image;
        LeftClickCommand = new RelayCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty));
        TrayBalloonTipClicked += OnBalloonTipClicked;

        ApplyTexts();
        _localizer.LanguageChanged += OnLanguageChanged;

        // Not part of any window: the icon has to be created explicitly. Efficiency mode stays off: it would lower
        // the priority of the whole process, and the window must appear at once.
        if (show)
        {
            ForceCreate(enablesEfficiencyMode: false);
        }
    }

    /// <summary>Left click on the icon, or menu « Open ».</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Menu « Customize ».</summary>
    public event EventHandler? CustomizeRequested;

    /// <summary>Menu « Quit ».</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Menu « Install update », or a click on the notification that announces an update.</summary>
    public event EventHandler? InstallUpdateRequested;

    /// <summary>
    /// Puts the entry « Install update <paramref name="version"/> » in the menu (replacing the one of an older
    /// version) and, with <paramref name="notify"/>, shows the notification « FolderSwitch <paramref name="version"/> is
    /// available »; a click on it raises <see cref="InstallUpdateRequested"/>.
    /// </summary>
    public void AnnounceUpdate(string version, bool notify)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);

        if (_closed)
        {
            return;
        }

        _updateVersion = version;
        ApplyTexts();
        UpdateSeparator.Visibility = Visibility.Visible;
        UpdateItem.Visibility = Visibility.Visible;
        UpdateItem.IsEnabled = !_downloading;

        if (!notify)
        {
            return;
        }

        try
        {
            // The notification can be refused by the system (notifications off): the menu entry remains.
            ShowNotification("FolderSwitch", _localizer.Format("Update.Available", version), NotificationIcon.None, sound: false);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Warn("The update notification could not be shown", e);
        }
    }

    /// <summary>
    /// Shows the notification that FolderSwitch is ready, with <paramref name="text"/> (« FolderSwitch is ready: press
    /// Ctrl + ⊞ Win », or that it was installed or updated): after the first-run wizard, the installer or the updater,
    /// the only sign that FolderSwitch runs. A click on it does nothing.
    /// </summary>
    public void ShowReady(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        if (_closed)
        {
            return;
        }

        try
        {
            // The notification can be refused by the system (notifications off): the shortcut works all the same.
            ShowNotification("FolderSwitch", text, NotificationIcon.None, sound: false);
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Warn("The ready notification could not be shown", e);
        }
    }

    /// <summary>Removes the menu entry of the update: the last check found FolderSwitch up to date.</summary>
    public void ClearUpdate()
    {
        _updateVersion = null;
        _downloading = false;
        UpdateSeparator.Visibility = Visibility.Collapsed;
        UpdateItem.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// While the update is downloaded, the menu entry reads « Downloading update… » and is disabled; back to
    /// « Install update x.y.z » when the download failed (<paramref name="downloading"/> <c>false</c>).
    /// </summary>
    public void SetUpdateDownloading(bool downloading)
    {
        if (_closed || _updateVersion is null)
        {
            return;
        }

        _downloading = downloading;
        ApplyTexts();
        UpdateItem.IsEnabled = !downloading;
    }

    /// <summary>Removes the icon from the notification area. Idempotent.</summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _localizer.LanguageChanged -= OnLanguageChanged;
        TrayBalloonTipClicked -= OnBalloonTipClicked;
        Dispose();
        _image.Dispose();
    }

    private void ApplyTexts()
    {
        OpenItem.Header = _localizer["Tray.Open"];
        CustomizeItem.Header = _localizer["Tray.Customize"];
        QuitItem.Header = _localizer["Tray.Quit"];
        if (_updateVersion is not null)
        {
            UpdateItem.Header = _downloading
                ? _localizer["Tray.UpdateDownloading"]
                : _localizer.Format("Tray.InstallUpdate", _updateVersion);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyTexts();

    private void OnOpenClick(object sender, RoutedEventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);

    private void OnCustomizeClick(object sender, RoutedEventArgs e) => CustomizeRequested?.Invoke(this, EventArgs.Empty);

    private void OnInstallUpdateClick(object sender, RoutedEventArgs e) => InstallUpdateRequested?.Invoke(this, EventArgs.Empty);

    private void OnQuitClick(object sender, RoutedEventArgs e) => QuitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A click on a notification: the one of an update opens its offer; the ready notification has nothing to open
    /// (unless an update was announced since, whose offer is then what the click opens).
    /// </summary>
    private void OnBalloonTipClicked(object sender, RoutedEventArgs e)
    {
        if (_updateVersion is not null)
        {
            InstallUpdateRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
