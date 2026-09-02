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

internal static class ThemeColor
{
    public static Color Pick(string light, string dark) =>
        Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? dark : light);
}

/// <summary>Delivery/proof status → foreground colour (badge text + border, list accent).</summary>
public sealed class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "Delivered" => ThemeColor.Pick("#3F6B4E", "#8FD0A4"),   // Ok
        "Failed" => ThemeColor.Pick("#A8443C", "#F0A49C"),      // Error
        "PickedUp" => ThemeColor.Pick("#8A6D1F", "#E4C579"),    // Warn/in-transit
        _ => ThemeColor.Pick("#595D6C", "#B2B6CA"),             // Muted
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Delivery/proof status → badge background tint.</summary>
public sealed class StatusTintConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "Delivered" => ThemeColor.Pick("#E3EFE6", "#22322A"),   // OkTint
        "Failed" => ThemeColor.Pick("#F8E5E3", "#3A2523"),      // ErrorTint
        "PickedUp" => ThemeColor.Pick("#F5EBD6", "#332B1B"),    // WarnTint
        _ => ThemeColor.Pick("#E7E9F2", "#2A2C33"),             // neutral
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Delivery/proof status → Turkish label for the badge.</summary>
public sealed class StatusLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "Delivered" => "Teslim edildi",
        "Failed" => "Teslim edilemedi",
        "PickedUp" => "Teslim alındı",
        _ => "Bekliyor",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
