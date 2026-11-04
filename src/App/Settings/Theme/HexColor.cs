using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Flect.Settings.Theme;

internal static class HexColor
{
    public static Color Parse(string? text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        var value = text.Trim().TrimStart('#');
        if (value.Length == 8) value = value[2..];
        if (value.Length == 3)
        {
            value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
        }
        if (value.Length != 6) return fallback;

        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : fallback;
    }

    public static string Format(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static string ForFfmpeg(string? text) =>
        "0x" + Format(Parse(text, Colors.Black))[1..];
}
