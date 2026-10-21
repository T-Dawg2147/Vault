using System.Globalization;
using Avalonia.Data.Converters;

namespace Vault.App.Views;

/// <summary>
/// Boolean converter. ConverterParameter="Inverse" negates the value and
/// ConverterParameter="Text" returns "Hide"/"Show" strings for the reveal toggle button.
/// (Visibility is expressed with IsVisible in Avalonia, so the default result is a bool.)
/// </summary>
public sealed class FlexibleBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        var mode = parameter as string;

        return mode switch
        {
            "Inverse" => !flag,
            "Text" => flag ? "Hide" : "Show",
            _ => flag
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && ((parameter as string) == "Inverse" ? !b : b);
}

/// <summary>Joins a string list (e.g. import row errors) into one line for grid display.</summary>
public sealed class StringListJoinConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is System.Collections.IEnumerable items
            ? string.Join("; ", items.Cast<object>())
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Bridges the view model's <see cref="DateTime"/>? to Avalonia's DatePicker <see cref="DateTimeOffset"/>?.</summary>
public sealed class DateTimeToOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? new DateTimeOffset(dt) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTimeOffset dto ? dto.DateTime : null;
}
