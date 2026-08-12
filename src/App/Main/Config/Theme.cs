using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Optimizer.Main.Config;

internal static class Theme
{
    private static readonly (string Key, string Light, string Dark)[] Palette =
    [
        ("Brush.Window", "#FFF6F6F6", "#FF1F1F1F"),
        ("Brush.Surface", "#FFFFFFFF", "#FF2B2B2B"),
        ("Brush.Border", "#FFD8D8D8", "#FF3D3D3D"),
        ("Brush.Text", "#FF1B1B1B", "#FFF2F2F2"),
        ("Brush.TextMuted", "#FF6B6B6B", "#FFA0A0A0"),
        ("Brush.Accent", "#FF0F6CBD", "#FF4CA0E0"),
        ("Brush.AccentText", "#FFFFFFFF", "#FF10202C"),
        ("Brush.Danger", "#FFC42B1C", "#FFFF6B5E"),
        ("Brush.Success", "#FF0F7B0F", "#FF6CCB6C"),
        ("Brush.Hover", "#FFEAEAEA", "#FF3A3A3A"),
        ("Brush.HoverSoft", "#FFE7F0FA", "#FF2B3947"),
        ("Brush.Pressed", "#FFD6D6D6", "#FF343434"),
        ("Brush.SurfaceLifted", "#FFFFFFFF", "#FF333333"),
        ("Brush.PrimaryHover", "#FF0A568F", "#FF6FBCF0"),
        ("Brush.PrimaryPressed", "#FF084672", "#FF5AA8DC"),
    ];

    // Color resources (not brushes) for use with transparent effects.
    private static readonly (string Key, string Light, string Dark)[] ShadowColors =
    [
        ("Color.Shadow", "#33000000", "#55000000"),
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
