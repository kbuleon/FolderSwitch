using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// The main window: the board of the configuration, normal use and Customize mode.
/// <para>
/// The child view models are projections of the configuration model. Every edit goes through
/// <see cref="BoardEditor"/> first, then the view model collections are brought in line with the model, then the
/// configuration is saved exactly once. A refused edit changes nothing and saves nothing.
/// </para>
/// <para>
/// Customize commands do nothing outside Customize mode, and nothing for a view model that is no longer on the
/// board (for instance one kept from before <see cref="Reload"/>). Meant to be used from the UI thread.
/// </para>
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const string ThisPcClsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string RecycleBinClsid = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    private const string SearchScheme = "search-ms:";

    private readonly IItemActivator _activator;
    private readonly IConfigPersistence _persistence;
    private readonly IUserPrompts _prompts;
    private readonly IWindowHost _host;
    private readonly Localizer _localizer;

    private AppConfig _config;
    private TabViewModel _activeTab;
    private CallContext _context = CallContext.None;
    private RenamableViewModel? _renaming;
    private bool _loading;
    private int _openPrompts;
    private bool _activating;
    private int _shownGeneration;

    /// <summary>The tab a drag started on, while the drag shows other tabs (<see cref="ShowTabWhileDragging"/>).</summary>
    private TabViewModel? _dragOrigin;

    /// <exception cref="ArgumentException"><paramref name="config"/> has no tab.</exception>
    public MainViewModel(
        AppConfig config,
        IItemActivator activator,
        IConfigPersistence persistence,
        IUserPrompts prompts,
        IWindowHost host,
        Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(activator);
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(localizer);
        RequireTabs(config);

        _activator = activator;
        _persistence = persistence;
        _prompts = prompts;
        _host = host;
        _localizer = localizer;

        Load(config);
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<TabViewModel> Tabs { get; } = new();

    /// <summary>
    /// The tab shown. Follows <c>Settings.ActiveTabId</c> (the first tab when that id is unknown or missing).
    /// Setting it to another tab of the board records its id and saves; any other value is ignored.
    /// </summary>
    public TabViewModel ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_loading || ReferenceEquals(value, _activeTab))
            {
                return;
            }

            if (value is null || !Tabs.Contains(value))
            {
                // A tab control may push null (or a tab being removed) while its items change: keep the current
                // tab and let the bindings read it again.
                OnPropertyChanged();
                return;
            }

            MakeActive(value);
            Save();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomizeLabel))]
    public partial bool IsCustomizing { get; set; }

    /// <summary>The window FolderSwitch was called from, set by <see cref="OnShown"/>.</summary>
    public CallContext Context
    {
        get => _context;
        private set
        {
            if (SetProperty(ref _context, value))
            {
                OnPropertyChanged(nameof(ContextLine));
                AddCurrentFolderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>The line at the bottom of the window; <c>null</c> (nothing shown) for any other window.</summary>
    public string? ContextLine => Context.Kind switch
    {
        CallContextKind.Explorer => _localizer["Context.Explorer"],
        CallContextKind.FileDialog => _localizer["Context.FileDialog"],
        _ => null,
    };

    /// <summary>The tab, group or item last clicked: the target of F2 (<see cref="RenameSelected"/>).</summary>
    [ObservableProperty]
    public partial object? Selected { get; set; }

    /// <summary>Label of the mode button: « Customize » in normal use, « Done » in Customize mode.</summary>
    public string CustomizeLabel => _localizer[IsCustomizing ? "Window.Done" : "Window.Customize"];

    private IEnumerable<IRelayCommand> CustomizeCommands =>
    [
        AddTabCommand, RemoveTabCommand, AddColumnCommand, RemoveColumnCommand, AddGroupCommand, RemoveGroupCommand,
        AddFolderCommand, AddFileCommand, AddCurrentFolderCommand, RemoveItemCommand, BeginRenameCommand,
        RenameSelectedCommand,
    ];

    // ----- Window life -----

    /// <summary>The window is about to appear: normal use, on the last active tab, for this calling window.</summary>
    public void OnShown(CallContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _shownGeneration++;
        Context = ctx;
        IsCustomizing = false;
        EndRename();
        Selected = null;
        ShowTab(ResolveActiveTab());
    }

    /// <summary>
    /// The shortcut was pressed while the window was on screen behind another window (Customize mode leaves it there):
    /// that window is the new calling window. The mode, the tab shown and the selection stay; a rename in progress ends
    /// (it was committed when the window lost the focus). An activation outcome still to come belongs to the previous
    /// call and is dropped.
    /// </summary>
    public void OnRecalled(CallContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _shownGeneration++;
        Context = ctx;
        EndRename();
    }

    /// <summary>The window lost the focus: it hides in normal use, unless one of its own prompts took the focus.</summary>
    public void OnDeactivated()
    {
        if (!IsCustomizing && _openPrompts == 0)
        {
            _host.HideWindow();
        }
    }

    /// <summary>
    /// Rebuilds the whole board from another configuration (after an import), which is edited from now on.
    /// The mode is kept; the rename in progress and the selection are dropped. Saves nothing.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="config"/> has no tab.</exception>
    public void Reload(AppConfig config)
    {
        RequireTabs(config);

        EndRename();
        Selected = null;
        Load(config);
        OnPropertyChanged(nameof(ActiveTab));
    }

    // ----- Normal use -----

    [RelayCommand]
    public void ToggleCustomize() => IsCustomizing = !IsCustomizing;

    [RelayCommand]
    public void Close() => _host.HideWindow();

    [RelayCommand]
    public void NextTab() => StepTab(1);

    [RelayCommand]
    public void PreviousTab() => StepTab(-1);

    /// <summary>Collapses or expands a group, in either mode, and saves at once.</summary>
    [RelayCommand]
    public void ToggleGroup(GroupViewModel? group)
    {
        if (!IsLive(group) || !Succeeded(BoardEditor.SetCollapsed(_config, group.Id, !group.IsCollapsed)))
        {
            return;
        }

        group.NotifyCollapsed();
        Save();
    }

    /// <summary>
    /// Click (or Ctrl+click) on an item in normal use: hides the window when it worked, otherwise shows why and
    /// stays open; when Windows already said why (its own message for a file it could not open), nothing more is
    /// shown. Ignored in Customize mode, for an item no longer on the board, and while a previous click is still
    /// being carried out. An outcome that arrives after the window was shown again (for another calling window) is
    /// dropped: it no longer concerns what is on screen.
    /// </summary>
    public Task ActivateItemAsync(ItemViewModel item, bool ctrl) =>
        RunActivationAsync(item, () => _activator.ActivateAsync(item.Model, Context, ctrl));

    /// <summary>
    /// « Open in File Explorer », from the item's menu: a new Explorer window on the folder (a file: on its folder),
    /// whatever the window FolderSwitch was called from. Hides the window or says why not, and is ignored, as a click is
    /// (<see cref="ActivateItemAsync"/>).
    /// </summary>
    public Task OpenInExplorerAsync(ItemViewModel item) =>
        RunActivationAsync(item, () => _activator.OpenInExplorerAsync(item.Model));

    /// <summary>
    /// Whether the item's menu offers « Open in a new tab »: in normal use, when a Ctrl+click on
    /// <paramref name="item"/> would open it in a new tab of the calling window (a folder, called from Explorer, on
    /// Windows 11). The entry then does what Ctrl+click does.
    /// </summary>
    public bool CanOpenInNewTab(ItemViewModel item) =>
        IsLive(item) && !IsCustomizing && _activator.CtrlOpensNewTab(item.Model.Kind, Context);

    /// <summary>Runs one activation of <paramref name="item"/> (see <see cref="ActivateItemAsync"/>) and acts on its outcome.</summary>
    private async Task RunActivationAsync(ItemViewModel item, Func<Task<ActivationOutcome>> activate)
    {
        if (!IsLive(item) || IsCustomizing || _activating)
        {
            return;
        }

        var shownGeneration = _shownGeneration;
        ActivationOutcome outcome;
        _activating = true;
        try
        {
            outcome = await activate();
        }
        finally
        {
            _activating = false;
        }

        if (shownGeneration != _shownGeneration)
        {
            return;
        }

        switch (outcome)
        {
            case ActivationOutcome.Done or ActivationOutcome.OpenedElsewhere:
                _host.HideWindow();
                break;
            case ActivationOutcome.ReportedByShell:
                break;
            case ActivationOutcome.TargetUnavailable:
                ShowError("Error.TargetUnavailable");
                break;
            default:
                ShowError("Error.ActionFailed");
                break;
        }
    }

    // ----- Customize mode: tabs -----

    /// <summary>Adds « New tab » with two empty columns, shows it and starts renaming it.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void AddTab()
    {
        if (!IsCustomizing)
        {
            return;
        }

        var tab = new TabViewModel(BoardEditor.AddTab(_config, _localizer["Tab.New"]), _localizer);
        Tabs.Add(tab);
        MakeActive(tab);
        StartRename(tab);
        Save();
    }

    /// <summary>
    /// Removes a tab, after a confirmation when it holds groups. The last tab cannot be removed. When the active
    /// tab goes, the next one (or the previous one, for the last) becomes active.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void RemoveTab(TabViewModel? tab)
    {
        if (!IsCustomizing || !IsLive(tab))
        {
            return;
        }

        // No question for the last tab: the editor refuses to remove it anyway.
        if (Tabs.Count > 1 && tab.HasGroups && !Confirm("Confirm.DeleteTab"))
        {
            return;
        }

        if (!Succeeded(BoardEditor.RemoveTab(_config, tab.Id)))
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        if (ReferenceEquals(tab, _activeTab))
        {
            MakeActive(index + 1 < Tabs.Count ? Tabs[index + 1] : Tabs[index - 1]);
        }

        Tabs.RemoveAt(index);
        ForgetDetached();
        Save();
    }

    /// <summary>Moves a tab to <paramref name="newIndex"/> (clamped), as a drag and drop does.</summary>
    public void MoveTab(TabViewModel tab, int newIndex)
    {
        if (!IsCustomizing || !IsLive(tab) || !Succeeded(BoardEditor.MoveTab(_config, tab.Id, newIndex)))
        {
            return;
        }

        Tabs.Move(Tabs.IndexOf(tab), _config.Tabs.IndexOf(tab.Model));
        Save();
    }

    // ----- Customize mode: columns -----

    /// <summary>Adds an empty column at the right of the active tab.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void AddColumn()
    {
        if (!IsCustomizing)
        {
            return;
        }

        var tab = _activeTab;
        tab.Columns.Add(new ColumnViewModel(BoardEditor.AddColumn(tab.Model), tab, _localizer));
        Save();
    }

    /// <summary>Removes a column; a column that still holds groups is refused with a message.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void RemoveColumn(ColumnViewModel? column)
    {
        if (!IsCustomizing || !IsLive(column) || !Succeeded(BoardEditor.RemoveColumn(column.Tab.Model, column.Model.Id)))
        {
            return;
        }

        column.Tab.Columns.Remove(column);
        ForgetDetached();
        Save();
    }

    /// <summary>Moves a column to <paramref name="newIndex"/> (clamped) of its tab, as a drag and drop does.</summary>
    /// <summary>
    /// Moves a column to <paramref name="newIndex"/> (clamped) of <paramref name="target"/>, its own tab or another
    /// one, as a drag and drop does. The last column of a tab does not leave it.
    /// </summary>
    public void MoveColumn(ColumnViewModel column, TabViewModel target, int newIndex)
    {
        if (!IsCustomizing || !IsLive(column) || !IsLive(target)
            || !Succeeded(BoardEditor.MoveColumn(_config, column.Model.Id, target.Id, newIndex)))
        {
            return;
        }

        var index = target.Model.Columns.IndexOf(column.Model);
        if (ReferenceEquals(column.Tab, target))
        {
            target.Columns.Move(target.Columns.IndexOf(column), index);
        }
        else
        {
            // The owner changes first, so that whatever reacts to the insertion sees the column where it now is.
            var source = column.Tab;
            column.Tab = target;
            source.Columns.Remove(column);
            target.Columns.Insert(index, column);
        }

        SaveMove();
    }

    /// <summary>
    /// Sets a fixed width (rounded to 10 and clamped to 150..520) or, with <c>null</c>, automatic width.
    /// Called once, at the end of a resize gesture.
    /// </summary>
    public void SetColumnWidth(ColumnViewModel column, int? width)
    {
        if (!IsCustomizing || !IsLive(column)
            || !Succeeded(BoardEditor.SetColumnWidth(column.Tab.Model, column.Model.Id, width)))
        {
            return;
        }

        column.NotifyWidth();
        Save();
    }

    // ----- Customize mode: groups -----

    /// <summary>
    /// Adds « New group » at the bottom of a column and starts renaming it. Its color is the next one of the
    /// palette: the number of groups already in the tab, modulo the palette size.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void AddGroup(ColumnViewModel? column)
    {
        if (!IsCustomizing || !IsLive(column))
        {
            return;
        }

        var model = BoardEditor.AddGroup(column.Model, _localizer["Group.New"], NextGroupColor(column.Tab));
        var group = new GroupViewModel(model, column, _localizer);
        column.Groups.Add(group);
        StartRename(group);
        Save();
    }

    /// <summary>Removes a group, after a confirmation when it holds items.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void RemoveGroup(GroupViewModel? group)
    {
        if (!IsCustomizing || !IsLive(group))
        {
            return;
        }

        if (group.Count > 0 && !Confirm("Confirm.DeleteGroup"))
        {
            return;
        }

        if (!Succeeded(BoardEditor.RemoveGroup(_config, group.Id)))
        {
            return;
        }

        group.Column.Groups.Remove(group);
        ForgetDetached();
        Save();
    }

    /// <summary>Moves a group to <paramref name="index"/> (clamped) of any column, in any tab.</summary>
    public void MoveGroup(GroupViewModel group, ColumnViewModel target, int index)
    {
        if (!IsCustomizing || !IsLive(group) || !IsLive(target)
            || !Succeeded(BoardEditor.MoveGroup(_config, group.Id, target.Model.Id, index)))
        {
            return;
        }

        var newIndex = target.Model.Groups.IndexOf(group.Model);
        if (ReferenceEquals(group.Column, target))
        {
            target.Groups.Move(target.Groups.IndexOf(group), newIndex);
        }
        else
        {
            // The owner changes first, so that whatever reacts to the insertion sees the group where it now is.
            var source = group.Column;
            group.Column = target;
            source.Groups.Remove(group);
            target.Groups.Insert(newIndex, group);
        }

        SaveMove();
    }

    /// <summary>Changes the header color; only a <c>#rrggbb</c> color is accepted.</summary>
    public void SetGroupColor(GroupViewModel group, string colorHex)
    {
        if (!IsCustomizing || !IsLive(group) || colorHex is not { Length: 7 } || !ColorMath.IsValidHex(colorHex)
            || !Succeeded(BoardEditor.SetGroupColor(_config, group.Id, colorHex)))
        {
            return;
        }

        group.NotifyColor();
        Save();
    }

    // ----- Customize mode: items -----

    /// <summary>Adds a folder chosen with the folder picker; nothing when the picker is cancelled.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void AddFolder(GroupViewModel? group)
    {
        if (!IsCustomizing || !IsLive(group))
        {
            return;
        }

        var path = WhilePromptOpen(_prompts.PickFolder);
        if (!string.IsNullOrWhiteSpace(path) && IsLive(group))
        {
            AddItem(group, new Item { Kind = ItemKind.Folder, Path = path });
        }
    }

    /// <summary>Adds a special folder by its reference; nothing when the group already holds it.</summary>
    public void AddSpecialFolder(GroupViewModel group, SpecialFolderKey key)
    {
        if (!IsCustomizing || !IsLive(group))
        {
            return;
        }

        var item = new Item { Kind = ItemKind.Folder, Special = key };
        if (!BoardEditor.ContainsTarget(group.Model, item))
        {
            AddItem(group, item);
        }
    }

    /// <summary>Adds a file chosen with the file picker; nothing when the picker is cancelled.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void AddFile(GroupViewModel? group)
    {
        if (!IsCustomizing || !IsLive(group))
        {
            return;
        }

        var path = WhilePromptOpen(_prompts.PickFile);
        if (!string.IsNullOrWhiteSpace(path) && IsLive(group))
        {
            AddItem(group, new Item { Kind = ItemKind.File, Path = path });
        }
    }

    /// <summary>
    /// Adds the current folder of the calling window. This PC and the Recycle Bin, seen as <c>::{CLSID}</c>, become
    /// special folders; anything else is kept as a path. Another virtual location (Home, a library...) is named as the
    /// shell shows it, when the context knows that name, since its parsing name means nothing to a reader. Only
    /// possible when the context has a current folder, and not for search results (<c>search-ms:</c>), which are no
    /// place to go back to.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddCurrentFolder))]
    public void AddCurrentFolder(GroupViewModel? group)
    {
        if (!CanAddCurrentFolder() || !IsLive(group) || Context.CurrentFolder is not { } folder)
        {
            return;
        }

        AddItem(group, ItemForCurrentFolder(folder, Context.CurrentFolderName));
    }

    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void RemoveItem(ItemViewModel? item)
    {
        if (!IsCustomizing || !IsLive(item) || !Succeeded(BoardEditor.RemoveItem(_config, item.Id)))
        {
            return;
        }

        item.Group.Items.Remove(item);
        ForgetDetached();
        Save();
    }

    /// <summary>Moves an item to <paramref name="index"/> (clamped) of any group.</summary>
    public void MoveItem(ItemViewModel item, GroupViewModel target, int index)
    {
        if (!IsCustomizing || !IsLive(item) || !IsLive(target)
            || !Succeeded(BoardEditor.MoveItem(_config, item.Id, target.Id, index)))
        {
            return;
        }

        var newIndex = target.Model.Items.IndexOf(item.Model);
        if (ReferenceEquals(item.Group, target))
        {
            target.Items.Move(target.Items.IndexOf(item), newIndex);
        }
        else
        {
            // The owner changes first, so that whatever reacts to the insertion sees the item where it now is.
            var source = item.Group;
            item.Group = target;
            source.Items.Remove(item);
            target.Items.Insert(newIndex, item);
        }

        SaveMove();
    }

    // ----- Customize mode: drag and drop across tabs -----

    /// <summary>
    /// A drag rests on another tab of the bar: shows that tab, without recording it, so that the drag can end in it.
    /// The tab the drag started on is remembered for <see cref="EndDrag"/>.
    /// </summary>
    public void ShowTabWhileDragging(TabViewModel tab)
    {
        if (!IsCustomizing || !IsLive(tab))
        {
            return;
        }

        _dragOrigin ??= _activeTab;
        ShowTab(tab);
    }

    /// <summary>
    /// A drag is over. When it showed other tabs and moved nothing, the tab it started on comes back; a move during the
    /// drag already recorded the tab it ended on (<see cref="SaveMove"/>).
    /// </summary>
    public void EndDrag()
    {
        if (_dragOrigin is { } origin)
        {
            _dragOrigin = null;
            if (IsLive(origin))
            {
                ShowTab(origin);
            }
        }
    }

    // ----- Customize mode: rename -----

    /// <summary>Starts renaming a tab, a group or an item (double-click); it also becomes the selection.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void BeginRename(object? target)
    {
        if (IsCustomizing && target is RenamableViewModel renamable && IsLive(renamable))
        {
            StartRename(renamable);
        }
    }

    /// <summary>
    /// Ends the rename of <paramref name="target"/> with <paramref name="text"/> (trimmed). A blank name is refused:
    /// the old name stays. The name shown, committed unchanged, edits and saves nothing: an item keeps its default
    /// name, which follows the language for a special folder. Does nothing when <paramref name="target"/> is not
    /// being renamed.
    /// </summary>
    public void CommitRename(object target, string text)
    {
        if (target is not RenamableViewModel { IsRenaming: true } renamable)
        {
            return;
        }

        var name = (text ?? string.Empty).Trim();
        if (string.Equals(name, renamable.CurrentName, StringComparison.Ordinal))
        {
            EndRename();
            return;
        }

        var result = BoardEditor.Rename(_config, renamable.Id, name);
        EndRename();
        if (!Succeeded(result))
        {
            return;
        }

        renamable.NotifyRenamed();
        Save();
    }

    /// <summary>Ends the rename of <paramref name="target"/>, keeping its name (Escape).</summary>
    [RelayCommand]
    public void CancelRename(object? target)
    {
        if (target is RenamableViewModel { IsRenaming: true })
        {
            EndRename();
        }
    }

    /// <summary>F2: renames the selected tab, group or item; without a selection, the tab shown.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    public void RenameSelected() => BeginRename(Selected ?? _activeTab);

    // ----- Internals -----

    partial void OnIsCustomizingChanged(bool value)
    {
        if (!value)
        {
            EndRename();
            Selected = null;
        }

        foreach (var command in CustomizeCommands)
        {
            command.NotifyCanExecuteChanged();
        }
    }

    private static void RequireTabs(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Tabs is not { Count: > 0 })
        {
            throw new ArgumentException("The configuration must have at least one tab.", nameof(config));
        }
    }

    private static string NextGroupColor(TabViewModel tab)
    {
        var palette = ThemeCatalog.GroupPalette;
        var groupCount = tab.Model.Columns.Sum(column => column.Groups.Count);
        return palette[groupCount % palette.Count].Hex;
    }

    private static Item ItemForCurrentFolder(string folder, string? shellName)
    {
        if (string.Equals(folder, ThisPcClsid, StringComparison.OrdinalIgnoreCase))
        {
            return new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.ThisPC };
        }

        if (string.Equals(folder, RecycleBinClsid, StringComparison.OrdinalIgnoreCase))
        {
            return new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.RecycleBin };
        }

        var name = IsVirtualLocation(folder) && !string.IsNullOrWhiteSpace(shellName) ? shellName.Trim() : null;
        return new Item { Kind = ItemKind.Folder, Path = folder, Name = name };
    }

    /// <summary>A location without a path: <c>::{CLSID}</c> (Home, a library...) or <c>shell:</c>.</summary>
    private static bool IsVirtualLocation(string folder) =>
        folder.StartsWith("::", StringComparison.Ordinal) || folder.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);

    /// <summary>Search results: a query, not a place.</summary>
    private static bool IsSearchResults(string folder) => folder.StartsWith(SearchScheme, StringComparison.OrdinalIgnoreCase);

    [MemberNotNull(nameof(_config), nameof(_activeTab))]
    private void Load(AppConfig config)
    {
        // While the tabs are rebuilt, a bound tab control may write ActiveTab: those writes are ignored.
        _loading = true;
        try
        {
            _config = config;
            _dragOrigin = null;
            Tabs.Clear();
            foreach (var tab in config.Tabs)
            {
                Tabs.Add(new TabViewModel(tab, _localizer));
            }

            _activeTab = ResolveActiveTab();
        }
        finally
        {
            _loading = false;
        }
    }

    private TabViewModel ResolveActiveTab()
    {
        var id = _config.Settings.ActiveTabId;
        return Tabs.FirstOrDefault(tab => tab.Id == id) ?? Tabs[0];
    }

    /// <summary>
    /// Shows a tab without recording it (the configuration already names it, or names none). A selection that belongs
    /// to another tab (that tab, or one of its groups or items) is no longer the one F2 should act on: it is dropped.
    /// </summary>
    private void ShowTab(TabViewModel tab)
    {
        if (ReferenceEquals(tab, _activeTab))
        {
            return;
        }

        EndRename();
        var selectedTab = Selected switch
        {
            TabViewModel selected => selected,
            GroupViewModel group => group.Column.Tab,
            ItemViewModel item => item.Group.Column.Tab,
            _ => null,
        };
        if (selectedTab is not null && !ReferenceEquals(selectedTab, tab))
        {
            Selected = null;
        }

        SetProperty(ref _activeTab, tab, nameof(ActiveTab));
    }

    /// <summary>Shows a tab and records it as the active one; the caller saves.</summary>
    private void MakeActive(TabViewModel tab)
    {
        ShowTab(tab);
        _config.Settings.ActiveTabId = tab.Id;
    }

    private void StepTab(int step)
    {
        var index = Tabs.IndexOf(_activeTab);
        ActiveTab = Tabs[(index + step + Tabs.Count) % Tabs.Count];
    }

    private bool CanCustomize() => IsCustomizing;

    private bool CanAddCurrentFolder() =>
        IsCustomizing && Context.CurrentFolder is { } folder && !string.IsNullOrWhiteSpace(folder) && !IsSearchResults(folder);

    private void AddItem(GroupViewModel group, Item item)
    {
        group.Items.Add(new ItemViewModel(BoardEditor.AddItem(group.Model, item), group, _localizer));
        Save();
    }

    private void StartRename(RenamableViewModel target)
    {
        EndRename();
        _renaming = target;
        target.IsRenaming = true;
        Selected = target;
    }

    private void EndRename()
    {
        if (_renaming is not null)
        {
            _renaming.IsRenaming = false;
            _renaming = null;
        }
    }

    /// <summary>After a removal: drops the rename and the selection when their object left the board.</summary>
    private void ForgetDetached()
    {
        if (_renaming is not null && !IsLive(_renaming))
        {
            EndRename();
        }

        if (Selected is RenamableViewModel selected && !IsLive(selected))
        {
            Selected = null;
        }
    }

    /// <summary>True when the edit went through; otherwise shows the matching message, if the refusal has one.</summary>
    private bool Succeeded(EditResult result)
    {
        if (result.Ok)
        {
            return true;
        }

        var messageKey = result.Error switch
        {
            EditError.ColumnNotEmpty => "Error.ColumnNotEmpty",
            EditError.LastTabCannotBeRemoved => "Error.LastTab",
            _ => null,
        };

        if (messageKey is not null)
        {
            ShowError(messageKey);
        }

        return false;
    }

    private bool Confirm(string messageKey) => WhilePromptOpen(() => _prompts.Confirm(messageKey));

    private void ShowError(string messageKey)
    {
        _openPrompts++;
        try
        {
            _prompts.ShowError(messageKey);
        }
        finally
        {
            _openPrompts--;
        }
    }

    /// <summary>
    /// Runs a modal prompt. A prompt takes the focus from the window: the deactivation this causes must not hide it
    /// (see <see cref="OnDeactivated"/>); <see cref="ShowError"/> does the same.
    /// </summary>
    private T WhilePromptOpen<T>(Func<T> prompt)
    {
        _openPrompts++;
        try
        {
            return prompt();
        }
        finally
        {
            _openPrompts--;
        }
    }

    private void Save() => _persistence.Save(_config);

    /// <summary>Saves a move; during a drag that showed other tabs, the tab shown becomes the active one.</summary>
    private void SaveMove()
    {
        if (_dragOrigin is not null)
        {
            _dragOrigin = null;
            _config.Settings.ActiveTabId = _activeTab.Id;
        }

        Save();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ContextLine));
        OnPropertyChanged(nameof(CustomizeLabel));
        foreach (var tab in Tabs)
        {
            tab.RefreshTexts();
        }
    }

    private bool IsLive([NotNullWhen(true)] TabViewModel? tab) => tab is not null && Tabs.Contains(tab);

    private bool IsLive([NotNullWhen(true)] ColumnViewModel? column) =>
        column is not null && IsLive(column.Tab) && column.Tab.Columns.Contains(column);

    private bool IsLive([NotNullWhen(true)] GroupViewModel? group) =>
        group is not null && IsLive(group.Column) && group.Column.Groups.Contains(group);

    private bool IsLive([NotNullWhen(true)] ItemViewModel? item) =>
        item is not null && IsLive(item.Group) && item.Group.Items.Contains(item);

    private bool IsLive(RenamableViewModel target) => target switch
    {
        TabViewModel tab => IsLive(tab),
        GroupViewModel group => IsLive(group),
        ItemViewModel item => IsLive(item),
        _ => false,
    };
}
