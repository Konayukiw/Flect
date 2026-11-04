using System.Text;

namespace Flect.Inspect;

internal static class FileName
{
    private static readonly HashSet<string> Decoys = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "heic", "heif", "ico", "gif", "svg", "bmp", "tif", "tiff",
        "mp4", "mov", "mkv", "m4a", "avi", "webm", "flv", "wmv",
        "mp3", "wav", "aif", "aiff", "aac", "ogg", "wma", "flac",
        "pdf", "zip", "7z", "tar", "rar", "gz", "tgz",
        "txt", "text", "json", "csv", "tsv", "xml", "html", "htm", "md", "markdown",
        "ini", "cfg", "conf", "log", "yaml", "yml", "toml", "css",
        "doc", "docx", "xls", "xlsx", "ppt", "pptx", "rtf", "odt", "ods", "odp",
        "srt", "vtt", "cs", "java", "py", "c", "cpp", "h", "rs", "go",
    };

    private static readonly Dictionary<char, (string Looks, string Name)> Confusable = new()
    {
        ['\u0430'] = ("a", "Cyrillic a"),
        ['\u0435'] = ("e", "Cyrillic ie"),
        ['\u043E'] = ("o", "Cyrillic o"),
        ['\u0440'] = ("p", "Cyrillic er"),
        ['\u0441'] = ("c", "Cyrillic es"),
        ['\u0443'] = ("y", "Cyrillic u"),
        ['\u0445'] = ("x", "Cyrillic ha"),
        ['\u0455'] = ("s", "Cyrillic dze"),
        ['\u0456'] = ("i", "Cyrillic i"),
        ['\u0458'] = ("j", "Cyrillic je"),
        ['\u04BB'] = ("h", "Cyrillic shha"),
        ['\u0501'] = ("d", "Cyrillic komi de"),
        ['\u051B'] = ("q", "Cyrillic qa"),
        ['\u051D'] = ("w", "Cyrillic we"),
        ['\u0391'] = ("A", "Greek Alpha"),
        ['\u0392'] = ("B", "Greek Beta"),
        ['\u0395'] = ("E", "Greek Epsilon"),
        ['\u0396'] = ("Z", "Greek Zeta"),
        ['\u0397'] = ("H", "Greek Eta"),
        ['\u0399'] = ("I", "Greek Iota"),
        ['\u039A'] = ("K", "Greek Kappa"),
        ['\u039C'] = ("M", "Greek Mu"),
        ['\u039D'] = ("N", "Greek Nu"),
        ['\u039F'] = ("O", "Greek Omicron"),
        ['\u03A1'] = ("P", "Greek Rho"),
        ['\u03A4'] = ("T", "Greek Tau"),
        ['\u03A5'] = ("Y", "Greek Upsilon"),
        ['\u03A7'] = ("X", "Greek Chi"),
        ['\u03B1'] = ("a", "Greek alpha"),
        ['\u03BF'] = ("o", "Greek omicron"),
        ['\u03C1'] = ("p", "Greek rho"),
    };

    public static NameInfo Read(string fileName)
    {
        var extension = ExtensionOf(fileName);
        var claimed = Kinds.Display(extension);
        var alerts = new List<string>();
        string? marked = null;
        string? shown = null;

        var bidi = new List<string>();
        var hidden = new List<string>();
        var markedName = new StringBuilder();
        foreach (var character in fileName)
        {
            if (BidiName(character) is string bidiName)
            {
                bidi.Add(Label(bidiName, character));
                markedName.Append('[').Append(bidiName).Append(']');
            }
            else if (HiddenName(character) is string hiddenName)
            {
                hidden.Add(Label(hiddenName, character));
                markedName.Append('[').Append(hiddenName).Append(']');
            }
            else
            {
                markedName.Append(character);
            }
        }

        if (bidi.Count > 0 || hidden.Count > 0)
        {
            marked = markedName.ToString();
            var visual = Visual(fileName);
            if (!string.Equals(visual, fileName, StringComparison.Ordinal)) shown = visual;
        }

        if (bidi.Count > 0)
        {
            alerts.Add(Loc.F("dialog.fileAnalyze.bidi", string.Join(", ", bidi.Distinct())));
        }
        if (hidden.Count > 0)
        {
            alerts.Add(Loc.F("dialog.fileAnalyze.zeroWidth", string.Join(", ", hidden.Distinct())));
        }

        var lookalikes = Lookalikes(fileName);
        if (lookalikes.Count > 0)
        {
            alerts.Add(Loc.F("dialog.fileAnalyze.homoglyph", string.Join(", ", lookalikes)));
        }

        return new NameInfo
        {
            Extension = extension,
            ClaimedKind = claimed,
            ShownName = shown,
            MarkedName = marked,
            DisguiseAs = Disguise(fileName, extension, shown),
            Alerts = alerts,
        };
    }

    private static string? Disguise(string fileName, string extension, string? shown)
    {
        if (!Kinds.IsExecutable(extension)) return null;

        if (!string.IsNullOrEmpty(shown))
        {
            var shownExtension = ExtensionOf(shown);
            if (shownExtension.Length > 0 &&
                !Kinds.IsExecutable(shownExtension) &&
                !string.Equals(shownExtension, extension, StringComparison.OrdinalIgnoreCase))
            {
                return Kinds.Display(shownExtension);
            }
        }

        var stem = fileName;
        var suffix = "." + extension;
        if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) stem = stem[..^suffix.Length];

        string? decoy = null;
        foreach (var part in stem.Split('.'))
        {
            if (Decoys.Contains(part)) decoy = Kinds.Display(part);
        }
        return decoy;
    }

    private static string ExtensionOf(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)) return "tar.gz";
        if (fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)) return "tgz";
        var dot = fileName.LastIndexOf('.');
        if (dot < 0 || dot == fileName.Length - 1) return string.Empty;
        return fileName[(dot + 1)..];
    }

    private static string Visual(string fileName)
    {
        var result = new StringBuilder();
        for (int index = 0; index < fileName.Length; index++)
        {
            var character = fileName[index];
            if (character == '\u202E')
            {
                var end = index + 1;
                while (end < fileName.Length && fileName[end] != '\u202C') end++;
                for (var reverse = end - 1; reverse > index; reverse--)
                {
                    if (BidiName(fileName[reverse]) is null && HiddenName(fileName[reverse]) is null)
                    {
                        result.Append(fileName[reverse]);
                    }
                }
                index = end;
                continue;
            }

            if (BidiName(character) is null && HiddenName(character) is null) result.Append(character);
        }
        return result.ToString();
    }

    private static List<string> Lookalikes(string fileName)
    {
        var found = new List<string>();
        var seen = new HashSet<int>();
        foreach (var token in fileName.Split('.'))
        {
            var ascii = false;
            foreach (var character in token)
            {
                if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    ascii = true;
                    break;
                }
            }

            foreach (var rune in token.EnumerateRunes())
            {
                if (!seen.Add(rune.Value)) continue;
                if (rune.Value is >= 0x1D400 and <= 0x1D7FF)
                {
                    found.Add($"U+{rune.Value:X} (mathematical alphanumeric)");
                    continue;
                }
                if (!rune.IsBmp) continue;
                var character = (char)rune.Value;
                if (Confusable.TryGetValue(character, out var confusable))
                {
                    if (!ascii) continue;
                    found.Add($"{character} (U+{(int)character:X4}, {confusable.Looks}, {confusable.Name})");
                    continue;
                }

                if (character is >= '\uFF21' and <= '\uFF3A')
                {
                    found.Add(FullWidth(character, (char)('A' + character - '\uFF21')));
                }
                else if (character is >= '\uFF41' and <= '\uFF5A')
                {
                    found.Add(FullWidth(character, (char)('a' + character - '\uFF41')));
                }
                else if (character is >= '\uFF10' and <= '\uFF19')
                {
                    found.Add(FullWidth(character, (char)('0' + character - '\uFF10')));
                }
            }
        }
        return found;
    }

    private static string FullWidth(char character, char looks) =>
        $"{character} (U+{(int)character:X4}, {looks}, fullwidth)";

    private static string Label(string name, char character) =>
        $"{name} (U+{(int)character:X4})";

    private static string? BidiName(char character) => character switch
    {
        '\u202A' => "LRE",
        '\u202B' => "RLE",
        '\u202C' => "PDF",
        '\u202D' => "LRO",
        '\u202E' => "RLO",
        '\u2066' => "LRI",
        '\u2067' => "RLI",
        '\u2068' => "FSI",
        '\u2069' => "PDI",
        _ => null,
    };

    private static string? HiddenName(char character) => character switch
    {
        '\u200B' => "ZWSP",
        '\u200C' => "ZWNJ",
        '\u200D' => "ZWJ",
        '\u2060' => "WJ",
        '\uFEFF' => "BOM",
        '\u00AD' => "SHY",
        _ => null,
    };
}
