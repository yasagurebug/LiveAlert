using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LiveAlert;

public sealed class ComplementColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            var r = (byte)Math.Round(color.Red * 255);
            var g = (byte)Math.Round(color.Green * 255);
            var b = (byte)Math.Round(color.Blue * 255);
            return Color.FromRgb((byte)(255 - r), (byte)(255 - g), (byte)(255 - b));
        }

        return Colors.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
