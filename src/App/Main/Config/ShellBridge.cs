using System.Globalization;
using Microsoft.Win32;

namespace Optimizer.Main.Config;

internal static class ShellBridge
{
    private const string Root = @"Software\Flect";
    private const string MenuKey = Root + @"\Menu";
    private const string PresetKey = Root + @"\Presets";

    public static void Publish(Settings settings)
    {
        try
        {
            using (var root = Registry.CurrentUser.CreateSubKey(Root))
            {
                root.SetValue("Language", Tag(settings.General.Language), RegistryValueKind.String);
            }

            using (var menu = Registry.CurrentUser.CreateSubKey(MenuKey))
            {
                menu.SetValue("Hidden", string.Join(';', settings.General.HiddenMenuItems),
                              RegistryValueKind.String);
            }

            using var presets = Registry.CurrentUser.CreateSubKey(PresetKey);
            var image = settings.Image.Presets;
            var video = settings.Video.Presets;

            Set(presets, "Image.Resize1", image.ResizeA);
            Set(presets, "Image.Resize2", image.ResizeB);
            Set(presets, "Image.Compress1", image.CompressA);
            Set(presets, "Image.Compress2", image.CompressB);
            Set(presets, "Image.Compress3", image.CompressC);
            Set(presets, "Image.Compress4", image.CompressD);
            Set(presets, "Image.Rotate1", image.RotateA);
            Set(presets, "Image.Rotate2", image.RotateB);
            Set(presets, "Image.Rotate3", image.RotateC);

            Set(presets, "Video.Resize1", video.ResizeA);
            Set(presets, "Video.Resize2", video.ResizeB);
            Set(presets, "Video.CompressDiscord", video.CompressDiscord);
            Set(presets, "Video.Compress1", video.CompressA);
            Set(presets, "Video.Compress2", video.CompressB);
            Set(presets, "Video.Compress3", video.CompressC);
            Set(presets, "Video.Rotate1", video.RotateA);
            Set(presets, "Video.Rotate2", video.RotateB);
            Set(presets, "Video.Rotate3", video.RotateC);
        }
        catch (Exception)
        {
        }
    }

    public static void Clear()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(MenuKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(PresetKey, throwOnMissingSubKey: false);

            using var root = Registry.CurrentUser.OpenSubKey(Root, writable: true);
            root?.DeleteValue("Language", throwOnMissingValue: false);
        }
        catch (Exception)
        {
        }
    }

    public static string Tag(AppLanguage language) => language switch
    {
        AppLanguage.English => "en",
        AppLanguage.Japanese => "ja",
        AppLanguage.ChineseSimplified => "zh-Hans",
        _ => "system",
    };

    private static void Set(RegistryKey key, string name, double value) =>
        key.SetValue(name, value.ToString("0.####", CultureInfo.InvariantCulture),
                     RegistryValueKind.String);

    private static void Set(RegistryKey key, string name, long value) =>
        key.SetValue(name, value.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
}
