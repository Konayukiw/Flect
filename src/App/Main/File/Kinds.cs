namespace Optimizer.Main.Inspect;

internal static class Kinds
{
    private static readonly HashSet<string> Text = new(StringComparer.OrdinalIgnoreCase)
    {
        "TXT", "TEXT", "JSON", "XML", "CSV", "TSV", "HTML", "MD", "INI", "CFG", "CONF",
        "CSS", "JS", "MJS", "CJS", "TS", "TSX", "JSX", "YAML", "YML", "TOML", "LOG",
        "CS", "JAVA", "KT", "PY", "RB", "GO", "RS", "PHP", "PL", "LUA", "SH", "BASH",
        "BAT", "CMD", "PS1", "PSM1", "SQL", "SRT", "VTT", "C", "H", "CPP", "HPP",
        "CC", "HH", "VB", "SWIFT", "R", "TEX", "RST", "ENV", "PROPS", "PROPERTIES",
        "GRADLE", "CMAKE",
    };

    private static readonly HashSet<string> Executable = new(StringComparer.OrdinalIgnoreCase)
    {
        "PE", "EXE", "SCR", "COM", "PIF", "BAT", "CMD", "JS", "JSE", "VBS", "VBE",
        "WSF", "WSH", "MSI", "MSP", "LNK", "PS1", "PSM1", "PSD1", "CPL", "HTA",
        "MSC", "DLL", "SYS", "GADGET", "APPLICATION",
    };

    public static string Display(string extension)
    {
        if (extension.Length == 0) return string.Empty;
        return extension.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "JPEG",
            "tif" or "tiff" => "TIFF",
            "heif" or "heic" => "HEIC",
            "htm" or "html" => "HTML",
            "yml" or "yaml" => "YAML",
            "text" or "txt" => "TXT",
            "aif" or "aiff" => "AIFF",
            "tar.gz" or "tgz" => "TAR.GZ",
            "markdown" => "MD",
            _ => extension.ToUpperInvariant(),
        };
    }

    public static bool Same(string claimed, string detected)
    {
        if (claimed.Length == 0) return true;
        if (detected.Length == 0) return false;
        if (string.Equals(claimed, detected, StringComparison.OrdinalIgnoreCase)) return true;
        if (Text.Contains(claimed) && Text.Contains(detected)) return true;
        if (Executable.Contains(claimed) && Executable.Contains(detected)) return true;
        return false;
    }

    public static bool IsExecutable(string extension) =>
        Executable.Contains(extension);

    public static bool IsTextExtension(string extension) =>
        extension.Length > 0 && Text.Contains(Display(extension));

    public static bool IsTextKind(string kind) => Text.Contains(kind);

    public static string YesNo(bool value) =>
        Loc.T(value ? "dialog.fileAnalyze.yes" : "dialog.fileAnalyze.no");
}
