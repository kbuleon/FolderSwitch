// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FolderSwitch.App.Views;

/// <summary><see cref="Visibility.Collapsed"/> for <c>null</c> (or an empty string), <see cref="Visibility.Visible"/> otherwise.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the two values are the same object: a tab and the active tab, say.</summary>
public sealed class SameReferenceConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [var first, var second] && first is not null && ReferenceEquals(first, second);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the two values are equal (<see cref="object.Equals(object, object)"/>): a choice and the value chosen.</summary>
public sealed class EqualValuesConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [var first, var second] && first is not null && first != DependencyProperty.UnsetValue && Equals(first, second);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// A label in capitals, as the mockups write the section titles of the menus. Invariant rules: the nine languages
/// write their capitals alike (no Turkish dotted I among them).
/// </summary>
public sealed class UpperCaseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string ?? string.Empty).ToUpper(CultureInfo.InvariantCulture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary><see cref="Visibility.Visible"/> for <c>false</c>, <see cref="Visibility.Collapsed"/> for <c>true</c>.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// The keys of a shortcut as the editor shows them, one cap each: <c>Ctrl + ⊞ Win</c> gives <c>Ctrl</c> and
/// <c>⊞ Win</c> (the separator of <c>HotkeyRules.Format</c>).
/// </summary>
public sealed class HotkeyPartsConverter : IValueConverter
{
    private const string Separator = " + ";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string ?? string.Empty).Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// <c>string.Format</c> of a localized text: the first value is the format (<c>{0} px</c>), the others its arguments.
/// Nothing while an argument is missing.
/// </summary>
public sealed class FormatConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [string format, .. var arguments] || arguments.Any(a => a is null || a == DependencyProperty.UnsetValue))
        {
            return string.Empty;
        }

        return string.Format(CultureInfo.CurrentCulture, format, arguments!);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary><c>!value</c>: a choice enabled while something is not true (a special folder not yet in the group).</summary>
public sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>
/// A step of the wizard (the parameter, its index) against the step shown (the value): <see cref="Visibility.Visible"/>
/// for that one, <see cref="Visibility.Hidden"/> for the others, which keep their room (the window does not change
/// size from step to step).
/// </summary>
public sealed class StepVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int step && int.TryParse(parameter as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && step == index
            ? Visibility.Visible
            : Visibility.Hidden;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// A radio button of one value (the parameter) bound to the value chosen: checked when they are equal; checking it
/// chooses its value, and unchecking it changes nothing (the button checked instead says what is chosen).
/// </summary>
public sealed class ValueIsParameterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}
