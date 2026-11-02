// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Themes;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// A group block, bound to a <see cref="Presentation.ViewModels.GroupViewModel"/>. Its colors depend on the group's
/// color and on the appearance (<see cref="ThemeKeys.Appearance"/>): they are computed again whenever either changes
/// (<see cref="Themes.GroupStyleSelector.Select"/>) and put in the block's own resources, which its template reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Header.</b> A click collapses or expands the group, in both modes (spec 5.3, 5.4). In Customize mode it also
/// selects the group (F2), except that a click on the name's text only selects it: the second click of a
/// double-click there renames the group in place, and a first click that had collapsed it would leave it collapsed
/// under the rename. The grip ⠿ starts a drag of the group (<see cref="BoardDragDrop"/>) past the system's threshold
/// and never collapses it; the color dot opens the palette and ✕ deletes the group, both buttons of their own. A
/// right-click on the header opens the group's <see cref="EditMenu"/> in Customize mode.
/// </para>
/// <para>
/// <b>Drop.</b> In Customize mode the group takes the items of any tab dropped on it while it is open, at the slot
/// under the mouse (the header counts as the top of the list); a collapsed group refuses them. A dragged group is left
/// to the column.
/// </para>
/// </remarks>
public partial class GroupView : UserControl
{
    public static readonly DependencyProperty GroupColorProperty = DependencyProperty.Register(
        nameof(GroupColor), typeof(string), typeof(GroupView), new PropertyMetadata(null, OnLookChanged));

    public static readonly DependencyProperty GroupNameProperty = DependencyProperty.Register(
        nameof(GroupName), typeof(string), typeof(GroupView), new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(
        nameof(IsCollapsed), typeof(bool), typeof(GroupView), new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty IsCustomizingProperty = DependencyProperty.Register(
        nameof(IsCustomizing), typeof(bool), typeof(GroupView), new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance), typeof(object), typeof(GroupView), new PropertyMetadata(null, OnAppearanceChanged));

    private static readonly DependencyPropertyKey TitlePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Title), typeof(string), typeof(GroupView), new PropertyMetadata(string.Empty));

    private static readonly DependencyPropertyKey ChevronAnglePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ChevronAngle), typeof(double), typeof(GroupView), new PropertyMetadata(OpenAngle));

    private static readonly DependencyPropertyKey CountVisibilityPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CountVisibility), typeof(Visibility), typeof(GroupView), new PropertyMetadata(Visibility.Collapsed));

    private static readonly DependencyPropertyKey CustomizeVisibilityPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CustomizeVisibility), typeof(Visibility), typeof(GroupView), new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty TitleProperty = TitlePropertyKey.DependencyProperty;

    public static readonly DependencyProperty ChevronAngleProperty = ChevronAnglePropertyKey.DependencyProperty;

    public static readonly DependencyProperty CountVisibilityProperty = CountVisibilityPropertyKey.DependencyProperty;

    public static readonly DependencyProperty CustomizeVisibilityProperty = CustomizeVisibilityPropertyKey.DependencyProperty;

    /// <summary>The chevron points down while the group is open, right while it is collapsed.</summary>
    private const double OpenAngle = 90;

    /// <summary>The dashes of « Add ▾ »: the group's color, half see-through.</summary>
    private const double AddBorderOpacity = 0.55;

    /// <summary>How far out of the block the selection ring runs (its margin, see the template).</summary>
    private const int RingOffset = 3;

    private bool _lookFailed;

    public GroupView()
    {
        // The appearance first, so that the block is drawn right from its first layout.
        SetResourceReference(AppearanceProperty, ThemeKeys.Appearance);
        SetBinding(GroupColorProperty, new Binding(nameof(GroupViewModel.Color)));
        SetBinding(GroupNameProperty, new Binding(nameof(GroupViewModel.Name)));
        SetBinding(IsCollapsedProperty, new Binding(nameof(GroupViewModel.IsCollapsed)));
        SetBinding(IsCustomizingProperty, new Binding($"{nameof(DataContext)}.{nameof(MainViewModel.IsCustomizing)}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(MainView), 1),
        });
        InitializeComponent();

        var header = PressGesture.For(Header);
        header.Click = _ => OnHeaderClick(header.PressedOn(NameRoom));
        header.DoubleClick = () => header.PressedOn(NameRoom) && BeginRename();
        Header.MouseRightButtonUp += OnHeaderRightClick;

        var grip = PressGesture.For(Grip);
        grip.Click = _ => Select();
        grip.CanDrag = () => IsCustomizing && DataContext is GroupViewModel { IsRenaming: false };
        grip.Drag = () =>
        {
            if (DataContext is GroupViewModel group)
            {
                var viewModel = ViewModel;
                BoardDragDrop.Start(this, group);
                viewModel?.EndDrag();
            }
        };
    }

    /// <summary>The list of the group's items: where a dragged item goes (the snapshots draw its indicator).</summary>
    internal ItemsControl Items => ItemsList;

    /// <summary>« Add ▾ »: the anchor of its menu.</summary>
    internal FrameworkElement AddAnchor => AddButton;

    /// <summary>The color dot: the anchor of the palette.</summary>
    internal FrameworkElement ColorDotAnchor => ColorDot;

    /// <summary>The group's color, <c>#rrggbb</c> (bound to the view model).</summary>
    public string? GroupColor
    {
        get => (string?)GetValue(GroupColorProperty);
        set => SetValue(GroupColorProperty, value);
    }

    /// <summary>The group's name (bound to the view model).</summary>
    public string GroupName
    {
        get => (string)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    /// <summary>Whether the group is collapsed (bound to the view model).</summary>
    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>Whether the window is in Customize mode (bound to the main view model).</summary>
    public bool IsCustomizing
    {
        get => (bool)GetValue(IsCustomizingProperty);
        set => SetValue(IsCustomizingProperty, value);
    }

    /// <summary>The appearance drawn (<see cref="AppearanceState"/>), from the resource <see cref="ThemeKeys.Appearance"/>.</summary>
    public object? Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>The name as shown: in capitals for the themes that write titles so.</summary>
    public string Title => (string)GetValue(TitleProperty);

    public double ChevronAngle => (double)GetValue(ChevronAngleProperty);

    public Visibility CountVisibility => (Visibility)GetValue(CountVisibilityProperty);

    /// <summary>What only Customize mode shows: the grip and the color dot.</summary>
    public Visibility CustomizeVisibility => (Visibility)GetValue(CustomizeVisibilityProperty);

    private static void OnLookChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GroupView)d).UpdateLook();

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GroupView)d).UpdateTitle();

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (GroupView)d;
        view.UpdateState();
        if (e.Property == IsCollapsedProperty)
        {
            // Paper draws its underline only under an open group.
            view.UpdateLook();
        }
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (GroupView)d;
        view.UpdateLook();
        view.UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = GroupName ?? string.Empty;
        var uppercase = Appearance is AppearanceState { Tokens.UppercaseTitles: true };
        SetValue(TitlePropertyKey, uppercase ? name.ToUpper(CultureInfo.CurrentCulture) : name);
    }

    private void UpdateState()
    {
        SetValue(ChevronAnglePropertyKey, IsCollapsed ? 0.0 : OpenAngle);
        SetValue(CountVisibilityPropertyKey, IsCollapsed || IsCustomizing ? Visibility.Visible : Visibility.Collapsed);
        SetValue(CustomizeVisibilityPropertyKey, IsCustomizing ? Visibility.Visible : Visibility.Collapsed);
    }

    /// <summary>
    /// Computes the block's colors and puts them in its resources, as one dictionary (the template is told once). A
    /// failure keeps the previous look and is logged once: a block drawn in the wrong colors is better than an error on
    /// every layout pass.
    /// </summary>
    private void UpdateLook()
    {
        if (Appearance is not AppearanceState appearance)
        {
            return;
        }

        try
        {
            var look = Themes.GroupStyleSelector.Select(appearance, GroupColor, IsCollapsed);
            var color = GroupColorOrFallback();
            Resources = new ResourceDictionary
            {
                ["Fs.Group.Color"] = ThemeApplier.SolidBrush(color),
                ["Fs.Group.AddBorder"] = ThemeApplier.SolidBrush(color, AddBorderOpacity),
                ["Fs.Group.RingRadius"] = new CornerRadius(appearance.RadiusPx + RingOffset),
                ["Fs.Group.HeaderBackground"] = look.HeaderBackground,
                ["Fs.Group.HeaderText"] = look.HeaderText,
                ["Fs.Group.Accent"] = look.Accent,
                ["Fs.Group.Body"] = look.BodyBackground,
                ["Fs.Group.FolderIcon"] = look.FolderIcon,
                ["Fs.Group.FileIcon"] = look.FileIcon,
                ["Fs.Group.ItemHover"] = look.ItemHover,
                ["Fs.Group.ItemText"] = look.ItemText,
                ["Fs.Group.ItemMuted"] = look.ItemMuted,
                ["Fs.Group.HeaderGlyph"] = look.HeaderGlyph,
                ["Fs.Group.CountText"] = look.CountText,
                ["Fs.Group.CountBackground"] = look.CountBackground,
                ["Fs.Group.DotRing"] = look.DotRing,
                ["Fs.Group.Frame"] = look.FrameBrush,
                ["Fs.Group.FrameThickness"] = look.FrameThickness,
                ["Fs.Group.ContentInset"] = look.ContentInset,
                ["Fs.Group.LeftAccentWidth"] = look.LeftAccentWidth,
                ["Fs.Group.TopAccentHeight"] = look.TopAccentHeight,
                ["Fs.Group.HeaderUnderline"] = look.HeaderUnderline,
                ["Fs.Group.HeaderPadding"] = look.HeaderPadding,
                ["Fs.Group.TitleFontSize"] = look.TitleFontSize,
                ["Fs.Group.Shadow"] = look.Shadow,
                ["Fs.Group.ShadowFill"] = look.ShadowFill,
                ["Fs.Group.TitleEffect"] = look.TitleEffect,
            };
            _lookFailed = false;
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            if (!_lookFailed)
            {
                _lookFailed = true;
                DebugLog.Error($"The colors of the group « {GroupName} » could not be computed", e);
            }
        }
    }

    private MainViewModel? ViewModel => MainView.Of(this)?.DataContext as MainViewModel;

    private void OnHeaderClick(bool onName)
    {
        if (ViewModel is not { } viewModel || DataContext is not GroupViewModel group)
        {
            return;
        }

        if (viewModel.IsCustomizing)
        {
            viewModel.Selected = group;
            if (onName)
            {
                return;
            }
        }

        viewModel.ToggleGroup(group);
    }

    private bool BeginRename()
    {
        if (ViewModel is not { IsCustomizing: true } viewModel || DataContext is not GroupViewModel group)
        {
            return false;
        }

        viewModel.BeginRename(group);
        return true;
    }

    /// <summary>In Customize mode, a right-click on the header selects the group and opens its <see cref="EditMenu"/>.</summary>
    private void OnHeaderRightClick(object sender, MouseButtonEventArgs e)
    {
        if (IsCustomizing && DataContext is GroupViewModel { IsRenaming: false } group && MainView.Of(this) is { } view)
        {
            Select();
            view.OpenEditMenu(group, Header);
            e.Handled = true;
        }
    }

    private void Select()
    {
        if (ViewModel is { IsCustomizing: true } viewModel && DataContext is GroupViewModel group)
        {
            viewModel.Selected = group;
        }
    }

    private void OnColorDotClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is GroupViewModel group)
        {
            MainView.Of(this)?.OpenPalette(group, ColorDot);
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is GroupViewModel group)
        {
            MainView.Of(this)?.OpenAddMenu(group, AddButton);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (BoardDragDrop.Payload<ItemViewModel>(e) is not { } item)
        {
            // A group: the column's to take.
            return;
        }

        e.Handled = true;
        if (!CanTake(item))
        {
            e.Effects = DragDropEffects.None;
            BoardDragDrop.HideIndicator();
            return;
        }

        var slot = BoardDragDrop.SlotAt(ItemsList, e.GetPosition(ItemsList), Orientation.Vertical);
        BoardDragDrop.ShowIndicator(ItemsList, slot, Orientation.Vertical);
        e.Effects = DragDropEffects.Move;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => BoardDragDrop.HideIndicatorIfLeft(this, e);

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (BoardDragDrop.Payload<ItemViewModel>(e) is not { } item)
        {
            return;
        }

        e.Handled = true;
        BoardDragDrop.HideIndicator();
        if (!CanTake(item) || DataContext is not GroupViewModel group || ViewModel is not { } viewModel)
        {
            return;
        }

        var slot = BoardDragDrop.SlotAt(ItemsList, e.GetPosition(ItemsList), Orientation.Vertical);
        var source = ReferenceEquals(item.Group, group) ? group.Items.IndexOf(item) : -1;
        if (!InsertionSlot.IsNoMove(slot, source))
        {
            viewModel.MoveItem(item, group, InsertionSlot.ToMoveIndex(slot, source));
        }
    }

    /// <summary>Only in Customize mode and while open; an item of any tab.</summary>
    private bool CanTake(ItemViewModel _) => IsCustomizing && !IsCollapsed && DataContext is GroupViewModel;

    private string GroupColorOrFallback() =>
        GroupColor is { } color && Core.Theming.ColorMath.IsValidHex(color) ? color : Themes.GroupStyleSelector.FallbackColor;
}
