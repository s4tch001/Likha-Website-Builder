using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WebsiteBuilder.App.Converters;

/// <summary>Converts a bool to Visibility, inverting the sense (true =&gt; Collapsed).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed or Visibility.Hidden;
}

/// <summary>Converts a count/collection emptiness to Visibility for empty-state messages.</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
