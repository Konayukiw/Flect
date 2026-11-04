using System.Globalization;

namespace Flect.Inspect.Suggest;

internal sealed class Suggest
{
    private const long MiB = 1024L * 1024L;

    private static readonly string[] ImageFormats = ["PNG", "JPG", "WEBP", "HEIC", "ICO", "GIF", "SVG"];
    private static readonly string[] VideoFormats = ["MP4", "MOV", "MKV", "M4A", "AVI", "WEBM", "FLV"];
    private static readonly string[] AudioFormats = ["MP3", "M4A", "WAV", "AIF", "AIFF", "AAC", "OGG", "WMA"];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "text", "md", "markdown", "log", "csv", "tsv", "ini", "cfg", "conf", "json", "xml",
        "yaml", "yml", "toml", "html", "htm", "css", "js", "mjs", "cjs", "ts", "tsx", "jsx",
        "c", "h", "cpp", "hpp", "cc", "hh", "cs", "java", "kt", "py", "rb", "go", "rs", "php",
        "pl", "lua", "sh", "bash", "bat", "cmd", "ps1", "psm1", "sql", "srt", "vtt", "asc",
        "tex", "rst", "env", "props", "properties", "gradle", "cmake", "m", "mm", "swift", "vb",
        "r", "jl",
    };

    private readonly string _path;
    private readonly long _length;
    private readonly HashSet<string> _hidden;
    private readonly ImagePresets _image;
    private readonly VideoPresets _video;
    private readonly Kind _kind;
    private readonly string? _format;
    private bool? _animated;

    internal Suggest(string path, long length, GeneralSettings general,
                            ImagePresets image, VideoPresets video)
    {
        _path = path;
        _length = length;
        _hidden = new HashSet<string>(general.HiddenMenuItems, StringComparer.OrdinalIgnoreCase);
        _image = image;
        _video = video;
        (_kind, _format) = Classify(path);
    }

    internal static bool CanCopy(string? key) => key is "dialog.fileAnalyze.path"
        or "dialog.fileAnalyze.linkTarget"
        or "dialog.fileAnalyze.zoneUrl"
        or "dialog.fileAnalyze.target";

    internal SuggestLink? Link(string? key) => key switch
    {
        "dialog.fileAnalyze.sizeOnDisk" => Compress(),
        "dialog.fileAnalyze.contents" => Contents(),
        "dialog.fileAnalyze.duration" => Trim(),
        "dialog.fileAnalyze.resolution" => _kind == Kind.Video ? Resize("video") : null,
        "dialog.fileAnalyze.dimensions" => _kind == Kind.Image ? Resize("image") : null,
        "dialog.fileAnalyze.encoding" => _kind == Kind.Text ? Choices("text.encode", EncodeChoices()) : null,
        "dialog.fileAnalyze.newlines" => _kind == Kind.Text ? Choices("text.lineEndings", EndingChoices()) : null,
        _ => null,
    };

    private SuggestLink? Compress()
    {
        if (_kind == Kind.Image) return ImageCompress();
        if (_kind == Kind.Video) return VideoCompress();
        return null;
    }

    private SuggestLink? ImageCompress()
    {
        var commands = new List<SuggestCommand>();
        var verb = "image.compress?bytes=";
        CompressPreset(commands, "image.compress.p1", _image.CompressA, 10 * MiB, verb);
        CompressPreset(commands, "image.compress.p2", _image.CompressB, 5 * MiB, verb);
        CompressPreset(commands, "image.compress.p3", _image.CompressC, 3 * MiB, verb);
        CompressPreset(commands, "image.compress.p4", _image.CompressD, 1 * MiB, verb);
        Add(commands, "image.compress.custom", Loc.T("menu.custom"), verb + "custom");
        return Menu("image.compress", "menu.image.compress", commands);
    }

    private SuggestLink? VideoCompress()
    {
        var commands = new List<SuggestCommand>();
        var discord = Positive(_video.CompressDiscord, VideoPresets.DiscordLimit);
        if (_length > discord)
        {
            Add(commands, "video.compress.discord",
                Loc.T("menu.video.compress.discord") + " (" + FormatBytes(discord) + ")",
                "video.compress?preset=discord");
        }
        var verb = "video.compress?bytes=";
        CompressPreset(commands, "video.compress.p1", _video.CompressA, 50 * MiB, verb);
        CompressPreset(commands, "video.compress.p2", _video.CompressB, 25 * MiB, verb);
        CompressPreset(commands, "video.compress.p3", _video.CompressC, 5 * MiB, verb);
        Add(commands, "video.compress.custom", Loc.T("menu.custom"), verb + "custom");
        return Menu("video.compress", "menu.video.compress", commands);
    }

    private void CompressPreset(List<SuggestCommand> commands, string id, long configured,
                                long fallback, string verb)
    {
        var bytes = Positive(configured, fallback);
        if (_length <= bytes) return;
        Add(commands, id, FormatBytes(bytes), verb + bytes.ToString(CultureInfo.InvariantCulture));
    }

    private SuggestLink? Resize(string domain)
    {
        var presets = domain == "image" ? (_image.ResizeA, _image.ResizeB) : (_video.ResizeA, _video.ResizeB);
        var commands = new List<SuggestCommand>();
        var verb = domain + ".resize?scale=";
        var first = Scale(presets.Item1, 50);
        var second = Scale(presets.Item2, 25);
        Add(commands, domain + ".resize.p1", first + "%", verb + first);
        Add(commands, domain + ".resize.p2", second + "%", verb + second);
        Add(commands, domain + ".resize.custom", Loc.T("menu.custom"), verb + "custom");
        return Menu(domain + ".resize", "menu." + domain + ".resize", commands);
    }

    private SuggestLink? Contents() => _kind switch
    {
        Kind.Image => Animated() ? null : Convert("image", ImageFormats, appendGif: false),
        Kind.Video => Convert("video", VideoFormats, appendGif: true),
        Kind.Audio => Convert("audio", AudioFormats, appendGif: false),
        Kind.Text => TextConvert(),
        Kind.Pdf => Convert("pdf", ["PNG", "JPG", "TXT"], appendGif: false, skipCurrent: false),
        Kind.Archive => Leaf("archive.extract", "menu.archive.extract"),
        _ => null,
    };

    private SuggestLink? Convert(string domain, IReadOnlyList<string> formats, bool appendGif,
                                 bool skipCurrent = true)
    {
        var commands = new List<SuggestCommand>();
        foreach (var format in formats)
        {
            if (skipCurrent && Same(format, _format)) continue;
            Add(commands, domain + ".convert." + format, "to " + format,
                domain + ".convert?to=" + format);
        }
        if (appendGif)
        {
            Add(commands, domain + ".convert.GIF", "to GIF", domain + ".convert?to=GIF");
        }
        return Menu(domain + ".convert", "menu." + domain + ".convert", commands);
    }

    private SuggestLink? TextConvert()
    {
        if (_format is null) return null;
        var commands = new List<SuggestCommand>();
        foreach (var target in TextTargets(_format))
        {
            Add(commands, "text.convert." + target, "to " + target, "text.convert?to=" + target);
        }
        return Menu("text.convert", "menu.text.convert", commands);
    }

    private SuggestLink? Trim() => _kind switch
    {
        Kind.Video => Leaf("video.trim", "menu.video.trim"),
        Kind.Audio => Leaf("audio.trim", "menu.audio.trim"),
        _ => null,
    };

    private SuggestLink? Choices(string id, IEnumerable<(string Child, string Label, string To)> choices)
    {
        var commands = new List<SuggestCommand>();
        foreach (var (child, label, to) in choices)
        {
            Add(commands, child, label, id + "?to=" + to);
        }
        return Menu(id, "menu." + id, commands);
    }

    private static IEnumerable<(string Child, string Label, string To)> EncodeChoices()
    {
        yield return ("text.encode.UTF8", "to UTF-8", "UTF8");
        yield return ("text.encode.UTF8BOM", "to UTF-8 (BOM)", "UTF8BOM");
        yield return ("text.encode.UTF16", "to UTF-16", "UTF16");
        yield return ("text.encode.SJIS", "to Shift_JIS", "SJIS");
    }

    private static IEnumerable<(string Child, string Label, string To)> EndingChoices()
    {
        yield return ("text.lineEndings.CRLF", "to CRLF (Windows)", "CRLF");
        yield return ("text.lineEndings.LF", "to LF (Unix)", "LF");
    }

    private static IEnumerable<string> TextTargets(string from)
    {
        if (!from.Equals("TXT", StringComparison.OrdinalIgnoreCase)) yield return "TXT";
        switch (from.ToUpperInvariant())
        {
            case "CSV":
                yield return "JSON";
                yield return "XML";
                yield return "HTML";
                yield return "MD";
                break;
            case "JSON":
                yield return "CSV";
                yield return "XML";
                break;
            case "XML":
                yield return "JSON";
                break;
            case "INI":
            case "CFG":
                yield return "JSON";
                yield return "XML";
                break;
        }
    }

    private bool Animated()
    {
        if (_format != "GIF") return false;
        return _animated ??= GifMotion.IsAnimated(_path);
    }

    private SuggestLink? Leaf(string id, string labelKey)
    {
        if (Hidden(id)) return null;
        var label = Loc.T(labelKey);
        return new SuggestLink(label, [new SuggestCommand(label, id)], true);
    }

    private SuggestLink? Menu(string id, string labelKey, List<SuggestCommand> commands)
    {
        if (Hidden(id) || commands.Count == 0) return null;
        return new SuggestLink(Loc.T(labelKey), commands, false);
    }

    private void Add(List<SuggestCommand> commands, string id, string label, string verb)
    {
        if (!Hidden(id)) commands.Add(new SuggestCommand(label, verb));
    }

    private bool Hidden(string id) => _hidden.Contains(id);

    private static bool Same(string format, string? current) =>
        current is not null && format.Equals(current, StringComparison.OrdinalIgnoreCase);

    private static long Positive(long value, long fallback) => value > 0 ? value : fallback;

    private static string Scale(double value, double fallback)
    {
        var chosen = double.IsFinite(value) && value > 0 ? value : fallback;
        return chosen.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";

        double scaled = bytes;
        var unit = 0;
        while (scaled >= 1024 && unit < units.Length - 1)
        {
            scaled /= 1024;
            unit++;
        }

        var decimals = scaled >= 100 ? 0 : scaled >= 10 ? 1 : 2;
        var text = Math.Round(scaled, decimals, MidpointRounding.AwayFromZero)
                       .ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture);
        if (text.Contains('.')) text = text.TrimEnd('0').TrimEnd('.');
        return text + " " + units[unit];
    }

    private static (Kind Kind, string? Format) Classify(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)) return (Kind.Archive, "TAR.GZ");

        var extension = Path.GetExtension(name);
        if (extension.Length <= 1) return (Kind.None, null);
        extension = extension[1..];

        return extension.ToLowerInvariant() switch
        {
            "pdf" => (Kind.Pdf, "PDF"),
            "zip" => (Kind.Archive, "ZIP"),
            "7z" => (Kind.Archive, "7Z"),
            "tar" => (Kind.Archive, "TAR"),
            "rar" => (Kind.Archive, "RAR"),
            "png" => (Kind.Image, "PNG"),
            "jpg" or "jpeg" => (Kind.Image, "JPG"),
            "webp" => (Kind.Image, "WEBP"),
            "heic" or "heif" => (Kind.Image, "HEIC"),
            "ico" => (Kind.Image, "ICO"),
            "gif" => (Kind.Image, "GIF"),
            "svg" => (Kind.Image, "SVG"),
            "mp4" => (Kind.Video, "MP4"),
            "mov" => (Kind.Video, "MOV"),
            "mkv" => (Kind.Video, "MKV"),
            "m4a" => (Kind.Video, "M4A"),
            "avi" => (Kind.Video, "AVI"),
            "webm" => (Kind.Video, "WEBM"),
            "flv" => (Kind.Video, "FLV"),
            "mp3" => (Kind.Audio, "MP3"),
            "wav" => (Kind.Audio, "WAV"),
            "aif" => (Kind.Audio, "AIF"),
            "aiff" => (Kind.Audio, "AIFF"),
            "aac" => (Kind.Audio, "AAC"),
            "ogg" => (Kind.Audio, "OGG"),
            "wma" => (Kind.Audio, "WMA"),
            _ when TextExtensions.Contains(extension) => (Kind.Text, TextFormat(extension)),
            _ => (Kind.None, null),
        };
    }

    private static string TextFormat(string extension) => extension.ToLowerInvariant() switch
    {
        "txt" or "text" => "TXT",
        "json" => "JSON",
        "csv" => "CSV",
        "xml" => "XML",
        "ini" => "INI",
        "cfg" or "conf" => "CFG",
        "css" => "CSS",
        "html" or "htm" => "HTML",
        "md" or "markdown" => "MD",
        _ => "TEXT",
    };

    private enum Kind { None, Image, Video, Audio, Text, Pdf, Archive }
}
