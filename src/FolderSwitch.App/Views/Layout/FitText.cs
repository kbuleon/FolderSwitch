// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// A text that sizes an automatic column (a group's title, an item's name, a count): when its text or its font size
/// or weight changes, it tells its column (<see cref="ColumnWidthPresenter.InvalidateColumnOf"/>), which fits itself
/// again. A plain <see cref="TextBlock"/> otherwise.
/// </summary>
public sealed class FitText : TextBlock
{
    static FitText()
    {
        // Added to TextBlock's own callbacks (the metadata is merged), for this class only.
        TextProperty.OverrideMetadata(typeof(FitText), new FrameworkPropertyMetadata(OnSizingChanged));
        FontSizeProperty.OverrideMetadata(typeof(FitText), new FrameworkPropertyMetadata(OnSizingChanged));
        FontWeightProperty.OverrideMetadata(typeof(FitText), new FrameworkPropertyMetadata(OnSizingChanged));
    }

    private static void OnSizingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ColumnWidthPresenter.InvalidateColumnOf(d);
}
