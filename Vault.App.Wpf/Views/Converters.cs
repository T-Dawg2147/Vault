using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vault.App.Wpf.Views;

/// <summary>
/// BooleanToVisibility variant supporting ConverterParameter="Inverse" (Collapsed when true)
/// and ConverterParameter="Text" returning "Hide"/"Show" strings for the reveal toggle button.
/// </summary>
public sealed class FlexibleBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        var mode = parameter as string;

        return mode switch
        {
            "Inverse" => flag ? Visibility.Collapsed : Visibility.Visible,
            "Text" => flag ? "Hide" : "Show",
            _ => flag ? Visibility.Visible : Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>Joins a string list (e.g. import row errors) into one line for grid display.</summary>
public sealed class StringListJoinConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is System.Collections.IEnumerable items
            ? string.Join("; ", items.Cast<object>())
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
