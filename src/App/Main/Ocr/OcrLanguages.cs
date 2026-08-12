using System.Diagnostics;
using System.Globalization;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Optimizer.Main.Ocr;

internal sealed record OcrLanguage(string Tag, string Name)
{
    public string Display => $"{Name} ({Tag})";
}

internal static class OcrLanguages
{
    public static readonly IReadOnlyList<string> Installable =
    [
        "en-US", "ja-JP", "zh-Hans-CN", "zh-Hant-TW", "ko-KR", "de-DE",
        "fr-FR", "es-ES", "it-IT", "pt-BR", "ru-RU", "nl-NL",
    ];

    public static IReadOnlyList<OcrLanguage> Installed()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages
                .Select(language => new OcrLanguage(language.LanguageTag, language.DisplayName))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static string Describe(string tag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(tag).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return tag;
        }
    }

    public static OcrEngine? Create(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return OcrEngine.TryCreateFromUserProfileLanguages();

        try
        {
            return OcrEngine.TryCreateFromLanguage(new Language(tag));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool StartInstaller(string tag)
    {
        var script =
            $"if (Get-Command Install-Language -ErrorAction SilentlyContinue) {{ " +
            $"Install-Language -Language '{tag}' }} else {{ " +
            $"Start-Process 'ms-settings:regionlanguage' }}";

        try
        {
            var info = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -NoExit -Command \"" +
                            script.Replace("\"", "\\\"") + "\"",
            };
            return System.Diagnostics.Process.Start(info) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
