using System.Globalization;

namespace Optimizer.Main;

internal static class Formatting
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long value)
    {
        if (value < 1024) return $"{value} B";

        double scaled = value;
        int unit = 0;
        while (scaled >= 1024 && unit < Units.Length - 1)
        {
            scaled /= 1024;
            unit++;
        }
        var digits = scaled >= 100 ? "0" : scaled >= 10 ? "0.0" : "0.00";
        return scaled.ToString(digits, CultureInfo.InvariantCulture) + " " + Units[unit];
    }

    public static string Count(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Percent(double fraction) =>
        (fraction * 100).ToString(fraction >= 0.1 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + "%";

    public static bool TryParseBytes(string text, out long bytes)
    {
        bytes = 0;
        var trimmed = text.Trim().Replace(",", string.Empty);
        if (trimmed.Length == 0) return false;

        long multiplier = 1;
        var suffixes = new (string Suffix, long Scale)[]
        {
            ("TB", 1L << 40), ("GB", 1L << 30), ("MB", 1L << 20), ("KB", 1L << 10),
            ("T", 1L << 40), ("G", 1L << 30), ("M", 1L << 20), ("K", 1L << 10), ("B", 1),
        };
        foreach (var (suffix, scale) in suffixes)
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                multiplier = scale;
                trimmed = trimmed[..^suffix.Length].Trim();
                break;
            }
        }

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
            !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        if (value <= 0) return false;

        var product = value * multiplier;
        if (product > long.MaxValue) return false;

        bytes = (long)product;
        return bytes > 0;
    }
}
