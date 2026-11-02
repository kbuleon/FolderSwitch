// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FolderSwitch.Core.Icons;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The icon picker of a tab or a group, on its <see cref="IconPickerViewModel"/>: a click on an emoji, on « Automatic »
/// or on « No icon » hands the choice to the view model, which saves it and closes the picker (<see cref="IconPickerViewModel.Closed"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyboard.</b> The filter box has the focus once the picker is shown. Enter chooses the first result while the
/// filter has words; Escape closes the picker without a change (the window closes the open flyout on Escape, see
/// <see cref="MainWindow"/>; the picker does it itself when the key does not reach the window); F2 does nothing.
/// </para>
/// <para>
/// <b>Cells.</b> The grid holds every emoji the font can draw (several hundred): its cells are plain elements, a
/// <see cref="Border"/> around a <see cref="TextBlock"/>, built here rather than by a template, and a click is told
/// from the grid itself (a press and a release on the same cell). Only the first category is built with the picker,
/// so that it shows at once; the others follow in the background (<see cref="CompleteGrid"/>). The cells of the results
/// are kept from one filter to the next and given their new emoji.
/// </para>
/// </remarks>
public partial class IconPicker : UserControl
{
    /// <summary>The emoji a category shows in the row, when the font draws it; otherwise its first one.</summary>
    private static readonly IReadOnlyDictionary<EmojiCategory, string> Representatives = new Dictionary<EmojiCategory, string>
    {
        [EmojiCategory.SmileysEmotion] = "😀",
        [EmojiCategory.PeopleBody] = "👋",
        [EmojiCategory.AnimalsNature] = "🐱",
        [EmojiCategory.FoodDrink] = "🍕",
        [EmojiCategory.TravelPlaces] = "✈",
        [EmojiCategory.Activities] = "⚽",
        [EmojiCategory.Objects] = "💡",
        [EmojiCategory.Symbols] = "✅",
    };

    private const char PresentationSelector = '\uFE0F';

    private readonly IconPickerViewModel _picker;
    private readonly Action _close;
    private readonly Style _cellStyle;
    private readonly Style _cellTextStyle;
    private readonly List<FrameworkElement> _sections = [];
    private readonly List<WrapPanel> _sectionCells = [];
    private readonly List<Button> _categories = [];
    private readonly List<Border> _resultCells = [];
    private Border? _currentCell;
    private Border? _pressed;
    private int _topCategory = -1;
    private int _built;

    /// <summary>The grid was moved since the picker opened (a wheel, its scroll bar, a category, a filter): the emoji in force is no longer brought into view.</summary>
    private bool _moved;

    internal IconPicker(IconPickerViewModel picker, ViewTexts texts, Action close)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        _picker = picker;
        _close = close;
        Texts = texts;
        DataContext = picker;
        InitializeComponent();

        _cellStyle = (Style)Resources["Cell"];
        _cellTextStyle = (Style)Resources["CellText"];
        Selection.SetIsSelected(AutomaticChoice, picker.Current == IconChoice.Automatic);
        Selection.SetIsSelected(NoneChoice, picker.Current == IconChoice.None);
        BuildSections();

        picker.PropertyChanged += OnPickerPropertyChanged;
        picker.Closed += (_, _) => _close();
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnLoaded;
        Scroller.PreviewMouseWheel += (_, _) => _moved = true;
        Scroller.PreviewMouseLeftButtonDown += (_, _) => _moved = true;
    }

    public ViewTexts Texts { get; }

    /// <summary>The snapshots: shows the picker filtered on <paramref name="text"/>, as if it had been typed.</summary>
    internal void Filter(string text) => FilterBox.Text = text;

    /// <summary>
    /// The categories, one under the other, each under its title, and their buttons in the row; the emojis of the first
    /// one only (<see cref="CompleteGrid"/> adds the others).
    /// </summary>
    private void BuildSections()
    {
        var titleStyle = (Style)Resources["SectionTitle"];
        var categoryStyle = (Style)Resources["CategoryButton"];
        foreach (var section in _picker.Sections)
        {
            var cells = new WrapPanel();
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Style = titleStyle, Text = section.Title.ToUpperInvariant() });
            panel.Children.Add(cells);
            SectionsPanel.Children.Add(panel);

            var button = new Button { Style = categoryStyle, Content = Representative(section), ToolTip = section.Title, Tag = _sections.Count };
            AutomationProperties.SetName(button, section.Title);
            button.Click += OnCategoryClick;
            CategoryRow.Children.Add(button);

            _sections.Add(panel);
            _sectionCells.Add(cells);
            _categories.Add(button);
        }

        BuildUpTo(0);
        MarkTopCategory(0);
    }

    /// <summary>
    /// Once the picker is shown: the emojis of the other categories, one category at a time, at
    /// <see cref="DispatcherPriority.Background"/> (a category the row scrolls to is built at once, with those before
    /// it); then <paramref name="completed"/>. Stops when the picker closed meanwhile.
    /// </summary>
    internal void CompleteGrid(Action? completed)
    {
        void Next()
        {
            if (Parent is null)
            {
                return;
            }

            if (_built == _sections.Count)
            {
                completed?.Invoke();
                return;
            }

            BuildUpTo(_built);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, Next);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, Next);
    }

    /// <summary>Builds the emojis of the categories up to <paramref name="index"/>, those not built yet.</summary>
    private void BuildUpTo(int index)
    {
        for (; _built <= index && _built < _sections.Count; _built++)
        {
            var cells = _sectionCells[_built];
            foreach (var entry in _picker.Sections[_built].Entries)
            {
                var cell = NewCell();
                if (Fill(cell, entry.Emoji) && _currentCell is null)
                {
                    // Shown once laid out (the layout comes first), unless the grid was moved meanwhile.
                    _currentCell = cell;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                    {
                        if (!_moved)
                        {
                            ShowCurrent();
                        }
                    });
                }

                cells.Children.Add(cell);
            }
        }
    }

    private static string Representative(IconSection section)
    {
        var wanted = Representatives.GetValueOrDefault(section.Category);
        var entry = section.Entries.FirstOrDefault(e => wanted is not null && SameEmoji(e.Emoji, wanted)) ?? section.Entries[0];
        return entry.Emoji;
    }

    private Border NewCell() => new() { Style = _cellStyle, Child = new TextBlock { Style = _cellTextStyle } };

    /// <summary>
    /// Gives <paramref name="cell"/> its emoji, its name (tooltip) and its mark when it is the icon in force; returns
    /// whether it is.
    /// </summary>
    private bool Fill(Border cell, string emoji)
    {
        ((TextBlock)cell.Child).Text = emoji;
        cell.Tag = emoji;
        var name = _picker.NameOf(emoji);
        cell.ToolTip = name;
        AutomationProperties.SetName(cell, name ?? emoji);

        var current = _picker.Current == IconChoice.Emoji && _picker.CurrentEmoji is { } chosen && SameEmoji(emoji, chosen);
        Selection.SetIsSelected(cell, current);
        return current;
    }

    /// <summary>The same emoji, with or without its presentation selector (U+FE0F).</summary>
    private static bool SameEmoji(string a, string b) =>
        string.Equals(a.TrimEnd(PresentationSelector), b.TrimEnd(PresentationSelector), StringComparison.Ordinal);

    /// <summary>Scrolls the grid to the emoji in force, if it is one (once the picker is laid out).</summary>
    internal void ShowCurrent() => _currentCell?.BringIntoView();

    private void OnLoaded(object sender, RoutedEventArgs e) => FilterBox.Focus();

    private void OnPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IconPickerViewModel.Results))
        {
            ShowResults();
        }
    }

    /// <summary>The filter changed: the results, under their number, in place of the categories; the categories again without words.</summary>
    private void ShowResults()
    {
        var filtering = _picker.IsFiltering;
        SectionsPanel.Visibility = filtering ? Visibility.Collapsed : Visibility.Visible;
        CategoryRow.Visibility = filtering ? Visibility.Hidden : Visibility.Visible;
        ResultsPanel.Visibility = filtering ? Visibility.Visible : Visibility.Collapsed;
        ResultsLabel.Visibility = filtering ? Visibility.Visible : Visibility.Collapsed;
        _moved = true;
        Scroller.ScrollToTop();
        if (!filtering)
        {
            // Already at the top, the scroller may tell no change: the first category is the one at the top.
            MarkTopCategory(0);
            return;
        }

        var results = _picker.Results;
        ResultsLabel.Text = results.Count == 0
            ? Texts["Icon.NoResult"]
            : string.Format(CultureInfo.CurrentCulture, Texts["Icon.Results"], results.Count);

        for (var i = 0; i < results.Count; i++)
        {
            if (i == _resultCells.Count)
            {
                var cell = NewCell();
                _resultCells.Add(cell);
                ResultsPanel.Children.Add(cell);
            }

            _ = Fill(_resultCells[i], results[i].Emoji);
            _resultCells[i].Visibility = Visibility.Visible;
        }

        for (var i = results.Count; i < _resultCells.Count; i++)
        {
            _resultCells[i].Visibility = Visibility.Collapsed;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                _close();
                break;
            case Key.Enter:
                e.Handled = true;
                if (_picker.IsFiltering)
                {
                    _picker.ChooseFirstResult();
                }

                break;
            case Key.F2:
                // Not the window's rename of the selection, under the picker.
                e.Handled = true;
                break;
        }
    }

    private void OnAutomaticClick(object sender, RoutedEventArgs e) => _picker.ChooseAutomatic();

    private void OnNoneClick(object sender, RoutedEventArgs e) => _picker.ChooseNone();

    private void OnCategoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: int index } && index < _sections.Count)
        {
            _moved = true;
            if (index >= _built)
            {
                BuildUpTo(index);
                SectionsPanel.UpdateLayout();
            }

            Scroller.ScrollToVerticalOffset(TopOf(_sections[index]));
            MarkTopCategory(index);
        }
    }

    /// <summary>The category at the top of the grid is underlined in the row.</summary>
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_picker.IsFiltering || _sections.Count == 0 || e.VerticalChange == 0)
        {
            return;
        }

        var offset = Scroller.VerticalOffset + 1;
        var top = 0;
        for (var i = 1; i < _sections.Count && TopOf(_sections[i]) <= offset; i++)
        {
            top = i;
        }

        // At the very end, the category clicked may be one the grid cannot bring to its top.
        if (Scroller.VerticalOffset < Scroller.ScrollableHeight || top > _topCategory)
        {
            MarkTopCategory(top);
        }
    }

    private double TopOf(FrameworkElement section) => section.TranslatePoint(default, SectionsPanel).Y;

    private void MarkTopCategory(int index)
    {
        if (index == _topCategory)
        {
            return;
        }

        _topCategory = index;
        for (var i = 0; i < _categories.Count; i++)
        {
            Selection.SetIsSelected(_categories[i], i == index);
        }
    }

    private void OnGridMouseDown(object sender, MouseButtonEventArgs e) => _pressed = CellOf(e.OriginalSource as DependencyObject);

    private void OnGridMouseUp(object sender, MouseButtonEventArgs e)
    {
        var cell = CellOf(e.OriginalSource as DependencyObject);
        var pressed = _pressed;
        _pressed = null;
        if (cell is { Tag: string emoji } && ReferenceEquals(cell, pressed))
        {
            e.Handled = true;
            _picker.Choose(emoji);
        }
    }

    /// <summary>The cell <paramref name="element"/> is in, if any.</summary>
    private Border? CellOf(DependencyObject? element)
    {
        for (var current = element; current is not null && !ReferenceEquals(current, GridHost); current = TreeHelpers.ParentOf(current))
        {
            if (current is Border { Tag: string } cell && ReferenceEquals(cell.Style, _cellStyle))
            {
                return cell;
            }
        }

        return null;
    }
}
