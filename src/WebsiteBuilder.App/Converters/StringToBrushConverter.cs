using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WebsiteBuilder.App.Converters;

/// <summary>
/// Converts a CSS colour string (hex or named) to a <see cref="Brush"/> for the
/// inspector colour swatch. Unparseable values fall back to transparent.
/// </summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(text);
                return new SolidColorBrush(color);
            }
            catch (FormatException)
            {
                // Not a recognised colour; fall through.
            }
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
