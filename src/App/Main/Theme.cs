using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Optimizer.Main;

internal static class Theme
{
    public static void Apply(Application application)
    {
        if (!IsDark()) return;

        var resources = application.Resources;
        resources["Brush.Window"] = Fill("#FF1F1F1F");
        resources["Brush.Surface"] = Fill("#FF2B2B2B");
        resources["Brush.Border"] = Fill("#FF3D3D3D");
        resources["Brush.Text"] = Fill("#FFF2F2F2");
        resources["Brush.TextMuted"] = Fill("#FFA0A0A0");
        resources["Brush.Accent"] = Fill("#FF4CA0E0");
        resources["Brush.AccentText"] = Fill("#FF10202C");
        resources["Brush.Danger"] = Fill("#FFFF6B5E");
        resources["Brush.Success"] = Fill("#FF6CCB6C");
        resources["Brush.Hover"] = Fill("#FF3A3A3A");
    }

    private static bool IsDark()
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
