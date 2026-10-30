using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using UtfUnknown;

namespace Optimizer.Main.Inspect;

internal static class ConfirmText
{
    private const long StructureLimit = 16L * 1024 * 1024;

    public static ProbeResult? Read(string path, string extension, CancellationToken token)
    {
        var info = new FileInfo(path);
        var sample = Sample(path);
        var detection = CharsetDetector.DetectFromStream(new MemoryStream(sample));
        var encoding = detection.Detected?.Encoding ?? Encoding.UTF8;
        var confidence = detection.Detected?.Confidence;

        string? text = null;
        NewlineCount counts;
        if (info.Length <= StructureLimit)
        {
            text = File.ReadAllText(path, encoding);
            counts = Count(text);
        }
        else
        {
            counts = CountStream(path, encoding, token);
        }

        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.encoding", encoding.EncodingName);
        if (confidence is not null)
        {
            bag.Add("dialog.fileAnalyze.confidence",
                    confidence.Value.ToString("0%", CultureInfo.InvariantCulture));
        }
        bag.Add("dialog.fileAnalyze.bom", Bom(sample));
        bag.Add("dialog.fileAnalyze.newlines", Newlines(counts));
        bag.Add("dialog.fileAnalyze.lines", counts.Lines.ToString("N0", CultureInfo.CurrentCulture));
        bag.Add("dialog.fileAnalyze.characters", counts.Characters.ToString("N0", CultureInfo.CurrentCulture));

        var sections = new List<FileSection>();
        var textSection = bag.Section("dialog.fileAnalyze.text");
        if (textSection is not null) sections.Add(textSection);

        var kind = "TEXT";
        if (text is null)
        {
            if (extension is "json" or "xml" or "html" or "htm" or "csv" or "tsv" or "md" or "markdown" or
                "ini" or "cfg" or "conf" or "srt" or "vtt")
            {
                var note = new FactBag();
                note.Add("dialog.fileAnalyze.tooLarge", Loc.T("dialog.fileAnalyze.tooLargeBody"));
                var extra = note.Section(SectionKey(extension));
                if (extra is not null) sections.Add(extra);
            }
        }
        else
        {
            kind = Structure(extension, text, sections) ?? kind;
        }

        return new ProbeResult { Kind = kind, Sections = sections };
    }

    private static string? Structure(string extension, string text, List<FileSection> sections)
    {
        string? kind = null;
        if (extension is "json" || LooksLikeJson(text))
        {
            sections.Add(Json(text));
            kind = "JSON";
        }
        else if (extension is "xml" ||
                 text.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            var xml = Xml(text);
            if (xml is not null)
            {
                sections.Add(xml);
                kind = "XML";
            }
        }

        if (extension is "csv" or "tsv")
        {
            sections.Add(Csv(text, extension == "tsv" ? '\t' : '\0'));
            kind ??= extension == "tsv" ? "TSV" : "CSV";
        }
        else if (extension is "html" or "htm")
        {
            var html = Html(text);
            if (html is not null) sections.Add(html);
            kind = "HTML";
        }
        else if (extension is "ini" or "cfg" or "conf")
        {
            sections.Add(Ini(text));
            kind ??= "INI";
        }
        else if (extension is "md" or "markdown")
        {
            sections.Add(Markdown(text));
            kind ??= "MD";
        }
        else if (extension is "srt" or "vtt" || LooksLikeSubtitle(text))
        {
            var cues = Subtitles(text);
            if (cues is not null)
            {
                sections.Add(cues);
                kind ??= extension == "vtt" ? "VTT" : "SRT";
            }
        }

        return kind;
    }

    private static FileSection Json(string text)
    {
        var bag = new FactBag();
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            bag.Add("dialog.fileAnalyze.valid", Kinds.YesNo(true));
            bag.Add("dialog.fileAnalyze.topLevel", Top(root.ValueKind));
            var count = root.ValueKind switch
            {
                JsonValueKind.Object => root.EnumerateObject().Count(),
                JsonValueKind.Array => root.GetArrayLength(),
                _ => -1,
            };
            if (count >= 0) bag.Add("dialog.fileAnalyze.count", count.ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.depth", Depth(root).ToString(CultureInfo.InvariantCulture));
        }
        catch (JsonException ex)
        {
            bag.Add("dialog.fileAnalyze.valid", Loc.F("dialog.fileAnalyze.invalid", ex.Message));
        }
        return bag.Section("dialog.fileAnalyze.jsonSection")!;
    }

    private static FileSection? Xml(string text)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            reader.MoveToContent();
            var bag = new FactBag();
            bag.Add("dialog.fileAnalyze.wellFormed", Kinds.YesNo(true));
            bag.Add("dialog.fileAnalyze.root", reader.Name);
            bag.Add("dialog.fileAnalyze.declaredEncoding", DeclaredEncoding(text));
            return bag.Section("dialog.fileAnalyze.xmlSection");
        }
        catch (XmlException ex)
        {
            var bag = new FactBag();
            bag.Add("dialog.fileAnalyze.wellFormed", Loc.F("dialog.fileAnalyze.invalid", ex.Message));
            return bag.Section("dialog.fileAnalyze.xmlSection");
        }
    }

    private static FileSection Csv(string text, char forced)
    {
        var delimiter = forced == '\0' ? Delimiter(text) : forced;
        var (rows, columns) = Measure(text, delimiter);
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.delimiter", delimiter == '\t' ? Loc.T("dialog.fileAnalyze.tab") : delimiter.ToString());
        bag.Add("dialog.fileAnalyze.rows", rows.ToString("N0", CultureInfo.CurrentCulture));
        bag.Add("dialog.fileAnalyze.columns", columns.ToString(CultureInfo.InvariantCulture));
        return bag.Section("dialog.fileAnalyze.csvSection")!;
    }

    private static FileSection? Html(string text)
    {
        var bag = new FactBag();
        var title = System.Text.RegularExpressions.Regex.Match(
            text, @"<title[^>]*>(.*?)</title>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (title.Success) bag.Add("dialog.fileAnalyze.metaTitle", title.Groups[1].Value.Trim());
        var charset = System.Text.RegularExpressions.Regex.Match(
            text, @"charset\s*=\s*[""']?\s*([^""'>\s]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (charset.Success) bag.Add("dialog.fileAnalyze.declaredEncoding", charset.Groups[1].Value);
        return bag.Section("dialog.fileAnalyze.htmlSection");
    }

    private static FileSection Ini(string text)
    {
        var sections = 0;
        var keys = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;
            if (line.StartsWith('[') && line.EndsWith(']')) sections++;
            else if (line.Contains('=') || line.Contains(':')) keys++;
        }
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.sections", sections.ToString(CultureInfo.InvariantCulture));
        bag.Add("dialog.fileAnalyze.keys", keys.ToString(CultureInfo.InvariantCulture));
        return bag.Section("dialog.fileAnalyze.iniSection")!;
    }

    private static FileSection Markdown(string text)
    {
        var headings = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimStart();
            var marks = 0;
            while (marks < line.Length && marks < 6 && line[marks] == '#') marks++;
            if (marks > 0 && marks < line.Length && line[marks] == ' ') headings++;
        }
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.headings", headings.ToString(CultureInfo.InvariantCulture));
        return bag.Section("dialog.fileAnalyze.markdown")!;
    }

    private static FileSection? Subtitles(string text)
    {
        string? first = null;
        string? last = null;
        var cues = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var arrow = line.IndexOf("-->", StringComparison.Ordinal);
            if (arrow <= 0) continue;
            cues++;
            var start = line[..arrow].Trim();
            var end = line[(arrow + 3)..].Trim().Split(' ')[0];
            first ??= start;
            last = end;
        }
        if (cues == 0) return null;
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.cues", cues.ToString(CultureInfo.InvariantCulture));
        bag.Add("dialog.fileAnalyze.starts", first);
        bag.Add("dialog.fileAnalyze.ends", last);
        return bag.Section("dialog.fileAnalyze.subtitle");
    }

    private static string SectionKey(string extension) => extension switch
    {
        "json" => "dialog.fileAnalyze.jsonSection",
        "xml" => "dialog.fileAnalyze.xmlSection",
        "html" or "htm" => "dialog.fileAnalyze.htmlSection",
        "csv" or "tsv" => "dialog.fileAnalyze.csvSection",
        "md" or "markdown" => "dialog.fileAnalyze.markdown",
        "srt" or "vtt" => "dialog.fileAnalyze.subtitle",
        _ => "dialog.fileAnalyze.iniSection",
    };

    private static string Top(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => Loc.T("dialog.fileAnalyze.object"),
        JsonValueKind.Array => Loc.T("dialog.fileAnalyze.array"),
        JsonValueKind.String => Loc.T("dialog.fileAnalyze.jsonString"),
        JsonValueKind.Number => Loc.T("dialog.fileAnalyze.jsonNumber"),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => kind.ToString(),
    };

    private static int Depth(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => 1 + element.EnumerateObject().Select(property => Depth(property.Value)).DefaultIfEmpty(0).Max(),
        JsonValueKind.Array => 1 + element.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0,
    };

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool LooksLikeSubtitle(string text)
    {
        var lines = 0;
        foreach (var raw in text.Split('\n'))
        {
            if (raw.Contains("-->", StringComparison.Ordinal)) return true;
            if (++lines > 40) break;
        }
        return false;
    }

    private static char Delimiter(string text)
    {
        var line = text.Split('\n', 2)[0];
        char best = ',';
        var score = -1;
        foreach (var candidate in new[] { ',', '\t', ';', '|' })
        {
            var count = line.Count(character => character == candidate);
            if (count > score)
            {
                score = count;
                best = candidate;
            }
        }
        return best;
    }

    private static (int Rows, int Columns) Measure(string text, char delimiter)
    {
        var rows = 0;
        var columns = 0;
        var field = 1;
        var quote = false;
        var any = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quote)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"') index++;
                    else quote = false;
                }
                any = true;
                continue;
            }

            if (character == '"')
            {
                quote = true;
                any = true;
                continue;
            }
            if (character == delimiter)
            {
                field++;
                any = true;
                continue;
            }
            if (character is '\n' or '\r')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                if (any)
                {
                    rows++;
                    columns = Math.Max(columns, field);
                }
                field = 1;
                any = false;
                continue;
            }
            any = true;
        }
        if (any)
        {
            rows++;
            columns = Math.Max(columns, field);
        }
        return (rows, columns);
    }

    private static string? DeclaredEncoding(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"encoding\s*=\s*[""']([^""']+)[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private readonly record struct NewlineCount(int Lines, long Characters, int Cr, int Lf, int Crlf);

    private static NewlineCount Count(string text)
    {
        var lines = 0;
        var cr = 0;
        var lf = 0;
        var crlf = 0;
        var pending = false;
        foreach (var character in text)
        {
            if (character == '\r')
            {
                if (pending) cr++;
                pending = true;
                continue;
            }
            if (character == '\n')
            {
                if (pending) crlf++;
                else lf++;
                pending = false;
                lines++;
                continue;
            }
            if (pending)
            {
                cr++;
                lines++;
                pending = false;
            }
        }
        if (pending)
        {
            cr++;
            lines++;
        }
        else if (text.Length > 0 && text[^1] is not '\n' and not '\r')
        {
            lines++;
        }
        return new NewlineCount(lines, text.Length, cr, lf, crlf);
    }

    private static NewlineCount CountStream(string path, Encoding encoding, CancellationToken token)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[8192];
        var lines = 0;
        var cr = 0;
        var lf = 0;
        var crlf = 0;
        long characters = 0;
        var pending = false;
        var last = '\0';
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            for (var index = 0; index < read; index++)
            {
                var character = buffer[index];
                characters++;
                last = character;
                if (character == '\r')
                {
                    if (pending) cr++;
                    pending = true;
                    continue;
                }
                if (character == '\n')
                {
                    if (pending) crlf++;
                    else lf++;
                    pending = false;
                    lines++;
                    continue;
                }
                if (pending)
                {
                    cr++;
                    lines++;
                    pending = false;
                }
            }
        }
        if (pending)
        {
            cr++;
            lines++;
        }
        else if (characters > 0 && last is not '\n' and not '\r')
        {
            lines++;
        }
        return new NewlineCount(lines, characters, cr, lf, crlf);
    }

    private static string Newlines(NewlineCount counts)
    {
        var kinds = 0;
        if (counts.Crlf > 0) kinds++;
        if (counts.Lf > 0) kinds++;
        if (counts.Cr > 0) kinds++;
        if (kinds == 0) return Loc.T("dialog.fileAnalyze.none");
        if (kinds > 1) return Loc.T("dialog.fileAnalyze.mixed");
        if (counts.Crlf > 0) return "CRLF";
        if (counts.Lf > 0) return "LF";
        return "CR";
    }

    private static string Bom(byte[] sample)
    {
        if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF) return "UTF-8";
        if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
        {
            return sample.Length >= 4 && sample[2] == 0 && sample[3] == 0 ? "UTF-32 LE" : "UTF-16 LE";
        }
        if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF) return "UTF-16 BE";
        if (sample.Length >= 4 && sample[0] == 0 && sample[1] == 0 && sample[2] == 0xFE && sample[3] == 0xFF)
        {
            return "UTF-32 BE";
        }
        return Loc.T("dialog.fileAnalyze.none");
    }

    private static byte[] Sample(string path)
    {
        using var stream = File.OpenRead(path);
        var length = (int)Math.Min(stream.Length, 64 * 1024);
        var buffer = new byte[length];
        stream.ReadExactly(buffer);
        return buffer;
    }
}
