using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Optimizer.Main;

internal static class ReportWriter
{
    private static readonly UTF8Encoding WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Plain = new(encoderShouldEmitUTF8Identifier: false);

    public static string Extension(ReportFormat format) => format switch
    {
        ReportFormat.Json => ".json",
        ReportFormat.Html => ".html",
        ReportFormat.Txt => ".txt",
        _ => ".csv",
    };

    public static string Filter(ReportFormat format) => format switch
    {
        ReportFormat.Json => "JSON|*.json",
        ReportFormat.Html => "HTML|*.html",
        ReportFormat.Txt => "Text|*.txt",
        _ => "CSV|*.csv",
    };

    public static void Write(AnalyzeReport report, string path, ReportFormat format)
    {
        var text = format switch
        {
            ReportFormat.Json => Json(report),
            ReportFormat.Html => Html(report),
            ReportFormat.Txt => Plain_(report),
            _ => Csv(report),
        };

        File.WriteAllText(path, text, format == ReportFormat.Csv ? WithBom : Plain);
    }

    private static string Csv(AnalyzeReport report)
    {
        var text = new StringBuilder();

        text.AppendLine("section,name,bytes,count,share");
        foreach (var root in report.Roots) Row(text, "root", root, null, null, null);

        Row(text, "summary", "total", report.TotalBytes, report.FileCount, null);
        Row(text, "summary", "folders", null, report.DirectoryCount, null);
        Row(text, "summary", "duplicateGroups", report.ReclaimableBytes, report.DuplicateGroups, null);
        Row(text, "summary", "duplicateFiles", null, report.DuplicateFiles, null);
        Row(text, "summary", "emptyFolders", null, report.EmptyDirectories.Count, null);

        foreach (var file in report.Largest) Row(text, "largest", file.Path, file.Size, null, null);

        double total = Math.Max(1, report.TotalBytes);
        foreach (var share in report.ByType)
        {
            Row(text, "type", share.Extension, share.Bytes, share.Count, share.Bytes / total);
        }

        foreach (var folder in report.EmptyDirectories) Row(text, "empty", folder, null, null, null);

        return text.ToString();
    }

    private static void Row(StringBuilder text, string section, string name, long? bytes,
                            int? count, double? share)
    {
        text.Append(Quote(section)).Append(',')
            .Append(Quote(name)).Append(',')
            .Append(bytes?.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(count?.ToString(CultureInfo.InvariantCulture)).Append(',')
            .AppendLine(share?.ToString("0.00000", CultureInfo.InvariantCulture));
    }

    private static string Quote(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;

    private static string Json(AnalyzeReport report)
    {
        var root = new JsonObject
        {
            ["roots"] = new JsonArray([.. report.Roots.Select(value => JsonValue.Create(value))]),
            ["totalBytes"] = report.TotalBytes,
            ["files"] = report.FileCount,
            ["folders"] = report.DirectoryCount,
            ["scanSeconds"] = Math.Round(report.Elapsed.TotalSeconds, 2),
            ["duplicates"] = new JsonObject
            {
                ["groups"] = report.DuplicateGroups,
                ["files"] = report.DuplicateFiles,
                ["reclaimableBytes"] = report.ReclaimableBytes,
            },
            ["largest"] = new JsonArray([.. report.Largest.Select(file => (JsonNode)new JsonObject
            {
                ["path"] = file.Path,
                ["bytes"] = file.Size,
            })]),
            ["byType"] = new JsonArray([.. report.ByType.Select(share => (JsonNode)new JsonObject
            {
                ["extension"] = share.Extension,
                ["files"] = share.Count,
                ["bytes"] = share.Bytes,
            })]),
            ["emptyFolders"] =
                new JsonArray([.. report.EmptyDirectories.Select(value => JsonValue.Create(value))]),
        };

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string Plain_(AnalyzeReport report)
    {
        var text = new StringBuilder();

        text.AppendLine(string.Join("   ", report.Roots));
        text.AppendLine();
        text.AppendLine($"{Formatting.Bytes(report.TotalBytes)} · " +
                        $"{Loc.N("count.file", report.FileCount)} · " +
                        $"{Loc.N("count.folder", report.DirectoryCount)}");
        text.AppendLine($"{Loc.N("count.group", report.DuplicateGroups)} · " +
                        $"{Formatting.Bytes(report.ReclaimableBytes)}");
        text.AppendLine();

        foreach (var file in report.Largest)
        {
            text.AppendLine($"{Formatting.Bytes(file.Size),12}  {file.Path}");
        }
        text.AppendLine();

        double total = Math.Max(1, report.TotalBytes);
        foreach (var share in report.ByType)
        {
            text.AppendLine($"{share.Extension,-16}{Formatting.Bytes(share.Bytes),12}  " +
                            $"{Formatting.Percent(share.Bytes / total),8}  " +
                            $"{Loc.N("count.file", share.Count)}");
        }
        text.AppendLine();

        foreach (var folder in report.EmptyDirectories) text.AppendLine(folder);
        return text.ToString();
    }

    private static string Html(AnalyzeReport report)
    {
        var html = new StringBuilder();
        double total = Math.Max(1, report.TotalBytes);

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html><head><meta charset=\"utf-8\">");
        html.AppendLine($"<title>{Escape(Branding.Name)} — {Escape(Loc.T("dialog.analyze.title"))}</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,system-ui,sans-serif;margin:32px;" +
                        "color:#1b1b1b}h2{font-size:15px;margin:26px 0 8px}" +
                        "table{border-collapse:collapse;width:100%;font-size:13px}" +
                        "td,th{text-align:left;padding:4px 10px;border-bottom:1px solid #e4e4e4}" +
                        "td.n{text-align:right;font-variant-numeric:tabular-nums}" +
                        "@media(prefers-color-scheme:dark){body{background:#1f1f1f;color:#f2f2f2}" +
                        "td,th{border-bottom-color:#3d3d3d}}</style>");
        html.AppendLine("</head><body>");

        html.AppendLine($"<h1>{Escape(Loc.T("dialog.analyze.title"))}</h1>");
        html.AppendLine($"<p>{Escape(string.Join("   ", report.Roots))}</p>");
        html.AppendLine($"<p>{Escape(Formatting.Bytes(report.TotalBytes))} · " +
                        $"{Escape(Loc.N("count.file", report.FileCount))} · " +
                        $"{Escape(Loc.N("count.folder", report.DirectoryCount))} · " +
                        $"{Escape(Loc.N("count.group", report.DuplicateGroups))} " +
                        $"({Escape(Formatting.Bytes(report.ReclaimableBytes))})</p>");

        html.AppendLine("<h2>Largest files</h2><table>");
        foreach (var file in report.Largest)
        {
            html.AppendLine($"<tr><td class=\"n\">{Escape(Formatting.Bytes(file.Size))}</td>" +
                            $"<td>{Escape(file.Path)}</td></tr>");
        }
        html.AppendLine("</table>");

        html.AppendLine("<h2>By type</h2><table>");
        foreach (var share in report.ByType)
        {
            html.AppendLine($"<tr><td>{Escape(share.Extension)}</td>" +
                            $"<td class=\"n\">{Escape(Formatting.Bytes(share.Bytes))}</td>" +
                            $"<td class=\"n\">{Escape(Formatting.Percent(share.Bytes / total))}</td>" +
                            $"<td class=\"n\">{Escape(Loc.N("count.file", share.Count))}</td></tr>");
        }
        html.AppendLine("</table>");

        if (report.EmptyDirectories.Count > 0)
        {
            html.AppendLine("<h2>Empty folders</h2><table>");
            foreach (var folder in report.EmptyDirectories)
            {
                html.AppendLine($"<tr><td>{Escape(folder)}</td></tr>");
            }
            html.AppendLine("</table>");
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
