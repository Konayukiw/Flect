using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Optimizer.Main.Config;

internal static class Theme
{
    private static readonly (string Key, string Light, string Dark)[] Palette =
    [
        ("Brush.Window", "#FFF6F7F9", "#FF16171D"),
        ("Brush.Surface", "#FFFFFFFF", "#FF1D1E26"),
        ("Brush.Border", "#FFE3E5EA", "#FF2C2E39"),
        ("Brush.Text", "#FF191C22", "#FFF1F2F6"),
        ("Brush.TextMuted", "#FF6B7280", "#FF9AA0AE"),
        ("Brush.Accent", "#FF4F46E5", "#FF818CF8"),
        ("Brush.AccentText", "#FFFFFFFF", "#FF14151D"),
        ("Brush.Danger", "#FFDC2626", "#FFF87171"),
        ("Brush.Success", "#FF16A34A", "#FF4ADE80"),
        ("Brush.Hover", "#FFEEF0F4", "#FF272935"),
        ("Brush.HoverSoft", "#FFEDEBFE", "#FF26263D"),
        ("Brush.Pressed", "#FFE2E4EA", "#FF23242E"),
        ("Brush.SurfaceLifted", "#FFFFFFFF", "#FF242630"),
        ("Brush.PrimaryHover", "#FF4338CA", "#FFA5B4FC"),
        ("Brush.PrimaryPressed", "#FF3730A3", "#FF909CF7"),
        ("Brush.MicaChrome", "#F8FFFFFF", "#F81D1E26"),
    ];

    private static readonly (string Key, string Light, string Dark)[] ShadowColors =
    [
        ("Color.Shadow", "#26000000", "#44000000"),
    ];

    public static bool IsDark { get; private set; }

    public static void Apply(Application application) =>
        Apply(application, Settings.Current.General.Theme);

    public static void Apply(Application application, AppTheme mode)
    {
        IsDark = mode switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => WindowsPrefersDark(),
        };

        var resources = application.Resources;
        foreach (var (key, light, dark) in Palette)
        {
            resources[key] = Fill(IsDark ? dark : light);
        }
        foreach (var (key, light, dark) in ShadowColors)
        {
            resources[key] = (Color)ColorConverter.ConvertFromString(IsDark ? dark : light);
        }
    }

    private static bool WindowsPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    private static SolidColorBrush Fill(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}

internal static class ColorText
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
