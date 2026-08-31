using System.Globalization;

namespace Ulak.Mobile.Converters;

/// <summary>True when the bound value is present: non-null, and for strings non-whitespace.</summary>
public sealed class IsPresentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => false,
        string s => !string.IsNullOrWhiteSpace(s),
        _ => true,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inverts a boolean.</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>Maps a delivery/proof status string to a colour.</summary>
public sealed class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "Delivered" => Colors.SeaGreen,
        "Failed" => Colors.IndianRed,
        _ => Colors.Gray,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
