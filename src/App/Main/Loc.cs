using System.Globalization;
using System.Windows.Markup;

namespace Optimizer.Main;

internal static class Loc
{
    private static IReadOnlyDictionary<string, string> _table = Strings.English;

    public static AppLanguage Current { get; private set; } = AppLanguage.English;

    public static void Use(AppLanguage language)
    {
        Current = Resolve(language);
        _table = Current switch
        {
            AppLanguage.Japanese => Strings.Japanese,
            AppLanguage.ChineseSimplified => Strings.ChineseSimplified,
            _ => Strings.English,
        };
    }

    public static AppLanguage Resolve(AppLanguage language)
    {
        if (language != AppLanguage.System) return language;

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "ja" => AppLanguage.Japanese,
            "zh" => AppLanguage.ChineseSimplified,
            _ => AppLanguage.English,
        };
    }

    public static string T(string key) =>
        _table.TryGetValue(key, out var value) ? value
        : Strings.English.TryGetValue(key, out var english) ? english
        : key;

    public static string T(string key, string fallback) =>
        _table.TryGetValue(key, out var value) ? value
        : Strings.English.TryGetValue(key, out var english) ? english
        : fallback;

    public static string F(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, T(key), arguments);

    public static string N(string key, long count)
    {
        var form = count == 1 && _table.TryGetValue(key + ".one", out var singular)
            ? singular
            : T(key);
        return string.Format(CultureInfo.CurrentCulture, form, Formatting.Count(count));
    }
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension()
    {
    }

    public TExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
