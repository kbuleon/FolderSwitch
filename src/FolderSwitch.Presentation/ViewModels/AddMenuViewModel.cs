// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using CommunityToolkit.Mvvm.ComponentModel;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.SpecialFolders;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>What the first line of the Add menu offers to add.</summary>
public enum AddMenuCurrent
{
    /// <summary>Nothing: the line is not shown (another window, or a browser page not read, or not read yet).</summary>
    None,

    /// <summary>The folder the calling Explorer window or file dialog shows.</summary>
    Folder,

    /// <summary>The page the calling browser shows.</summary>
    Url,
}

/// <summary>
/// The Add menu of a group in Customize mode: a first line that adds what the calling window shows (its folder, or the
/// page of a browser), a grey footer that says where FolderSwitch was called from, then the other additions, which
/// are <see cref="MainViewModel"/>'s.
/// <para>
/// A browser's page cannot be read at the call itself (the budget of the call is short): the menu reads it while it is
/// open, from the thread pool, one attempt every 200 ms, for 2 s at most, and stops at the first success. The result is
/// published on the synchronization context the menu was opened on. After <see cref="Dispose"/>, nothing is attempted
/// or published any more. Meant to be created and used from the UI thread (<see cref="MainViewModel.OpenAddMenu"/>):
/// it needs the synchronization context of that thread to publish, and without one a browser page is not read at all.
/// </para>
/// </summary>
public sealed class AddMenuViewModel : ObservableObject, IDisposable
{
    /// <summary>The pause between two attempts to read the page.</summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>How long, from the opening of the menu, the page is waited for.</summary>
    internal static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);

    private readonly MainViewModel _main;
    private readonly GroupViewModel _group;
    private readonly IBrowserAddressReader _reader;
    private readonly TimeProvider _time;
    private readonly Localizer _localizer;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly CancellationTokenSource _stop = new();
    private readonly long _opened;

    private AddMenuCurrent _currentKind;
    private string? _currentDetail;
    private bool _currentAlreadyPresent;
    private bool _isReading;
    private BrowserPage? _page;
    private volatile bool _disposed;

    internal AddMenuViewModel(
        MainViewModel main,
        GroupViewModel group,
        CallContext context,
        IBrowserAddressReader reader,
        TimeProvider time,
        Localizer localizer)
    {
        _main = main;
        _group = group;
        _reader = reader;
        _time = time;
        _localizer = localizer;
        _opened = time.GetTimestamp();

        switch (context)
        {
            case { Kind: CallContextKind.Explorer }:
                Footer = _localizer["AddMenu.FromExplorer"];
                ShowFolder(context);
                break;
            case { Kind: CallContextKind.FileDialog }:
                Footer = _localizer["AddMenu.FromFileDialog"];
                ShowFolder(context);
                break;
            case { Kind: CallContextKind.Browser, Browser: { } browser }:
                Footer = _localizer.Format("AddMenu.FromBrowser", browser.ToString());

                // The result is published on the context of the opening: without one there is nowhere to show it.
                if (_ui is not null)
                {
                    _isReading = true;
                    var window = context.Window;
                    _ = Task.Run(() => ReadAsync(window, browser, _stop.Token));
                }

                break;
        }
    }

    /// <summary>What the first line offers; <see cref="AddMenuCurrent.None"/> hides it.</summary>
    public AddMenuCurrent CurrentKind
    {
        get => _currentKind;
        private set => SetProperty(ref _currentKind, value);
    }

    /// <summary>The folder's path (its name, for a place without a path) or the page's address, shown in grey under the first line.</summary>
    public string? CurrentDetail
    {
        get => _currentDetail;
        private set => SetProperty(ref _currentDetail, value);
    }

    /// <summary>True when the group already holds what the first line offers: the line is greyed out and checked.</summary>
    public bool CurrentAlreadyPresent
    {
        get => _currentAlreadyPresent;
        private set => SetProperty(ref _currentAlreadyPresent, value);
    }

    /// <summary>True while the page of a browser is being read.</summary>
    public bool IsReading
    {
        get => _isReading;
        private set => SetProperty(ref _isReading, value);
    }

    /// <summary>Where FolderSwitch was called from; <c>null</c> (no footer) for any other window.</summary>
    public string? Footer { get; }

    /// <summary>The special folders of the catalog and whether the group holds them, as the group lists them.</summary>
    public IReadOnlyList<SpecialFolderState> SpecialFolders => _group.SpecialFolderStates;

    /// <summary>
    /// The page the first line offers, when the group does not hold it yet; <c>null</c> otherwise, and once disposed.
    /// The view takes it before closing the menu: adding it opens the address prompt, which must wait until the menu is
    /// gone.
    /// </summary>
    public BrowserPage? CurrentPage =>
        !_disposed && CurrentKind == AddMenuCurrent.Url && !CurrentAlreadyPresent ? _page : null;

    /// <summary>
    /// Adds what the first line offers, a page once its address is confirmed (see
    /// <see cref="MainViewModel.AddCurrentUrl"/>); nothing when it offers nothing or the group already holds it.
    /// </summary>
    public void AddCurrent()
    {
        if (_disposed || CurrentAlreadyPresent)
        {
            return;
        }

        switch (CurrentKind)
        {
            case AddMenuCurrent.Folder:
                _main.AddCurrentFolder(_group);
                break;
            case AddMenuCurrent.Url when _page is { } page:
                _main.AddCurrentUrl(_group, page);
                break;
        }
    }

    /// <summary>Asks for a folder with the folder picker and adds it.</summary>
    public void AddFolder()
    {
        if (!_disposed)
        {
            _main.AddFolder(_group);
        }
    }

    /// <summary>Asks for a file with the file picker and adds it.</summary>
    public void AddFile()
    {
        if (!_disposed)
        {
            _main.AddFile(_group);
        }
    }

    /// <summary>Asks for a web address and adds it.</summary>
    public void AddUrl()
    {
        if (!_disposed)
        {
            _main.AddUrl(_group);
        }
    }

    /// <summary>Adds a separator at the end of the group (never « already present »: a group may hold several).</summary>
    public void AddSeparator()
    {
        if (!_disposed)
        {
            _main.AddSeparator(_group);
        }
    }

    /// <summary>Adds a special folder of <see cref="SpecialFolders"/>.</summary>
    public void AddSpecialFolder(SpecialFolderKey key)
    {
        if (!_disposed)
        {
            _main.AddSpecialFolder(_group, key);
        }
    }

    /// <summary>Ends the reading of the page: nothing is attempted or published from now on. May be called again.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isReading = false;
        _stop.Cancel();
        _stop.Dispose();
    }

    private void ShowFolder(CallContext context)
    {
        if (MainViewModel.ItemForCurrentFolder(context) is not { } item)
        {
            return;
        }

        CurrentKind = AddMenuCurrent.Folder;
        CurrentDetail = item.Special is { } special
            ? _localizer[SpecialFolderCatalog.All.First(info => info.Key == special).NameKey]
            : item.Name ?? item.Path;
        CurrentAlreadyPresent = BoardEditor.ContainsTarget(_group.Model, item);
    }

    /// <summary>
    /// Runs on the thread pool: attempts to read the page until one succeeds or <see cref="ReadTimeout"/> has passed
    /// since the menu opened, every <see cref="RetryInterval"/>. The reader never throws; a cancellation ends it.
    /// </summary>
    private async Task ReadAsync(nint window, BrowserKind browser, CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested && _time.GetElapsedTime(_opened) < ReadTimeout)
            {
                var page = _reader.TryRead(window, browser);

                if (page is not null)
                {
                    // An attempt may take long: a page that comes after the deadline is not shown.
                    if (_time.GetElapsedTime(_opened) < ReadTimeout)
                    {
                        PostToUi(() => ShowPage(page));
                    }

                    return;
                }

                await Task.Delay(RetryInterval, _time, stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The menu was closed.
        }
        finally
        {
            PostToUi(() => IsReading = false);
        }
    }

    private void ShowPage(BrowserPage page)
    {
        _page = page;
        CurrentDetail = page.Url;
        CurrentAlreadyPresent = BoardEditor.ContainsTarget(_group.Model, new Item { Kind = ItemKind.Url, Url = page.Url });
        CurrentKind = AddMenuCurrent.Url;
    }

    /// <summary>Runs <paramref name="publish"/> on the context of the opening, unless the menu is disposed by then.</summary>
    private void PostToUi(Action publish) =>
        _ui!.Post(
            _ =>
            {
                if (!_disposed)
                {
                    publish();
                }
            },
            null);
}
