using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Optimizer.Main;

namespace Optimizer.Tasks;

internal sealed class TextConvert(TaskRequest request) : BatchTask(request)
{
    private static readonly UTF8Encoding CsvEncoding = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding PlainEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private string _target = "JSON";

    public override string Title => Loc.T("menu.text.convert");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "JSON").ToUpperInvariant();
        return true;
    }

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var text = Text.Read(path);
        var source = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        var converted = _target == "TXT" ? text : Render(Parse(source, text, progress, path));
        var output = OutputPath.Derive(path, string.Empty, "." + _target.ToLowerInvariant());
        using var working = new WorkingFile(output);
        File.WriteAllText(output, converted, _target == "CSV" ? CsvEncoding : PlainEncoding);
        working.Keep();
    }, progress.Token);

    private static JsonNode Parse(string source, string text, ITaskProgress progress,
                                  string path) => source switch
    {
        "JSON" => JsonNode.Parse(text)
                  ?? throw new InvalidOperationException(Loc.T("msg.emptyFile")),
        "CSV" => FromCsv(text, progress, path),
        "XML" => FromXml(text),
        "INI" or "CONF" or "CFG" => FromIni(text),
        _ => throw new InvalidOperationException(Loc.F("msg.cannotConvertFrom", source)),
    };

    private static readonly JsonSerializerOptions JsonLayout = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private string Render(JsonNode tree) => _target switch
    {
        "JSON" => tree.ToJsonString(JsonLayout),
        "XML" => ToXml(tree),
        "CSV" => ToCsv(AsTable(tree)),
        "HTML" => ToHtml(AsTable(tree)),
        "MD" => ToMarkdown(AsTable(tree)),
        _ => throw new InvalidOperationException(Loc.F("msg.cannotConvertTo", _target)),
    };

    private static readonly char[] Delimiters = [',', ';', '\t', '|'];

    private const int DetectionWindow = 64 * 1024;

    private static char DetectDelimiter(string text)
    {
        var window = text.Length > DetectionWindow ? text[..DetectionWindow] : text;

        var consistent = Delimiters
            .Select(candidate => (candidate, columns: ConsistentColumns(window, candidate)))
            .Where(result => result.columns > 1)
            .ToList();

        var quoted = QuoteVote(window);
        if (quoted is char c && consistent.Any(result => result.candidate == c)) return c;

        return consistent.Count > 0
            ? consistent.MaxBy(result => result.columns).candidate
            : ',';
    }

    private static char? QuoteVote(string text)
    {
        var votes = new Dictionary<char, int>();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '"') continue;
            if (!inQuotes) { inQuotes = true; continue; }
            if (i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }

            inQuotes = false;
            if (i + 1 < text.Length && Delimiters.Contains(text[i + 1]))
            {
                votes[text[i + 1]] = votes.GetValueOrDefault(text[i + 1]) + 1;
            }
        }
        return votes.Count == 0 ? null : votes.MaxBy(vote => vote.Value).Key;
    }

    private static int ConsistentColumns(string text, char delimiter)
    {
        var rows = SplitCsv(text, delimiter)
            .Where(row => !(row.Count == 1 && row[0].Length == 0))
            .Take(10)
            .ToList();

        if (rows.Count == 0) return 0;
        int columns = rows[0].Count;
        return rows.All(row => row.Count == columns) ? columns : 0;
    }

    private static List<List<string>> SplitCsv(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool any = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c != '"') { field.Append(c); continue; }
                if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; continue; }
                quoted = false;
                continue;
            }

            if (c == '"')
            {
                quoted = true;
                any = true;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                any = true;
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
                any = false;
            }
            else
            {
                field.Append(c);
                any = true;
            }
        }

        if (any || field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    private static JsonNode FromCsv(string text, ITaskProgress progress, string path)
    {
        var delimiter = DetectDelimiter(text);
        if (delimiter != ',')
        {

            var shown = delimiter == '\t' ? Loc.T("msg.separator.tab") : delimiter.ToString();
            progress.Info(Loc.F("msg.readAsSeparated", Path.GetFileName(path), shown));
        }

        var rows = SplitCsv(text, delimiter);
        if (rows.Count == 0) throw new InvalidOperationException(Loc.T("msg.emptyFile"));

        var headers = rows[0];
        var table = new JsonArray();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count == 1 && row[0].Length == 0) continue;

            var record = new JsonObject();
            for (int column = 0; column < headers.Count; column++)
            {
                var name = headers[column].Length > 0 ? headers[column] : $"column{column + 1}";
                record[name] = column < row.Count ? row[column] : string.Empty;
            }
            table.Add(record);
        }

        if (table.Count == 0)
        {
            throw new InvalidOperationException(Loc.T("msg.noDataRows"));
        }
        return table;
    }

    private static JsonArray AsTable(JsonNode tree)
    {
        if (tree is JsonArray array && array.All(item => item is JsonObject)) return array;

        throw new InvalidOperationException(Loc.T("msg.notATable"));
    }

    private static IReadOnlyList<string> ColumnsOf(JsonArray rows)
    {
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.OfType<JsonObject>())
        {
            foreach (var pair in row)
            {
                if (seen.Add(pair.Key)) columns.Add(pair.Key);
            }
        }
        return columns;
    }

    private static string Cell(JsonObject row, string column) =>
        row.TryGetPropertyValue(column, out var value) && value is not null
            ? value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString()
            : string.Empty;

    private static string ToCsv(JsonArray rows)
    {
        var columns = ColumnsOf(rows);
        var text = new StringBuilder();

        text.AppendLine(string.Join(',', columns.Select(Quote)));
        foreach (var row in rows.OfType<JsonObject>())
        {
            text.AppendLine(string.Join(',', columns.Select(column => Quote(Cell(row, column)))));
        }
        return text.ToString();

        static string Quote(string value) =>
            value.AsSpan().IndexOfAny(",\"\r\n") >= 0
                ? '"' + value.Replace("\"", "\"\"") + '"'
                : value;
    }

    private static string ToHtml(JsonArray rows)
    {
        var columns = ColumnsOf(rows);
        var html = new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html><head><meta charset=\"utf-8\"><title>Table</title></head><body>");
        html.AppendLine("<table>");
        html.AppendLine("  <thead><tr>" +
                        string.Concat(columns.Select(c => $"<th>{Escape(c)}</th>")) + "</tr></thead>");
        html.AppendLine("  <tbody>");
        foreach (var row in rows.OfType<JsonObject>())
        {
            html.AppendLine("    <tr>" +
                            string.Concat(columns.Select(c => $"<td>{Escape(Cell(row, c))}</td>")) +
                            "</tr>");
        }
        html.AppendLine("  </tbody>");
        html.AppendLine("</table>");
        html.AppendLine("</body></html>");
        return html.ToString();

        static string Escape(string value) => value
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static string ToMarkdown(JsonArray rows)
    {
        var columns = ColumnsOf(rows);
        var markdown = new StringBuilder();

        markdown.AppendLine("| " + string.Join(" | ", columns.Select(Escape)) + " |");
        markdown.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");
        foreach (var row in rows.OfType<JsonObject>())
        {
            markdown.AppendLine("| " +
                string.Join(" | ", columns.Select(c => Escape(Cell(row, c)))) + " |");
        }
        return markdown.ToString();

        static string Escape(string value) =>
            value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }


    private static string ToXml(JsonNode tree)
    {
        var root = new XElement("root");
        Fill(root, tree);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var text = new Utf8StringWriter();
        using (var writer = XmlWriter.Create(text,
                   new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
        {
            document.Save(writer);
        }
        return text.ToString();

        static void Fill(XElement parent, JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var pair in o)
                    {
                        var child = new XElement(SafeName(pair.Key));
                        Fill(child, pair.Value);
                        parent.Add(child);
                    }
                    break;

                case JsonArray a:
                    foreach (var item in a)
                    {
                        var child = new XElement("item");
                        Fill(child, item);
                        parent.Add(child);
                    }
                    break;

                case null:
                    break;

                default:
                    parent.Value = node.GetValueKind() == JsonValueKind.String
                        ? node.GetValue<string>()
                        : node.ToJsonString();
                    break;
            }
        }
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    private static string SafeName(string key)
    {
        var name = new StringBuilder();
        foreach (var c in key)
        {
            name.Append(char.IsLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_');
        }
        if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_')) name.Insert(0, '_');
        return name.ToString();
    }

    private static JsonNode FromXml(string text)
    {
        var document = XDocument.Parse(text);
        if (document.Root is null) throw new InvalidOperationException(Loc.T("msg.noRootElement"));
        return Convert(document.Root);

        static JsonNode Convert(XElement element)
        {
            var children = element.Elements().ToList();
            if (children.Count == 0 && !element.HasAttributes)
            {
                return JsonValue.Create(element.Value)!;
            }

            var node = new JsonObject();
            foreach (var attribute in element.Attributes())
            {
                node["@" + attribute.Name.LocalName] = JsonValue.Create(attribute.Value);
            }

            foreach (var group in children.GroupBy(child => child.Name.LocalName))
            {
                var items = group.ToList();
                node[group.Key] = items.Count == 1
                    ? Convert(items[0])
                    : new JsonArray(items.Select(Convert).ToArray());
            }

            var own = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
            if (own.Length > 0) node["#text"] = JsonValue.Create(own);

            return node;
        }
    }

    private static JsonNode FromIni(string text)
    {
        var root = new JsonObject();
        JsonObject current = root;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                var section = line[1..^1].Trim();
                if (root[section] is JsonObject existing) { current = existing; continue; }

                current = [];
                root[section] = current;
                continue;
            }

            var split = line.IndexOf('=');
            if (split < 0) continue;

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();
            if (key.Length > 0) current[key] = JsonValue.Create(value);
        }
        return root;
    }
}
