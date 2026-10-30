using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Optimizer.Main.Inspect;

internal static class FileReportWriter
{
    private static readonly UTF8Encoding WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UTF8Encoding Plain = new(encoderShouldEmitUTF8Identifier: false);

    public static void Write(FileReport report, string? sha256, string path, ReportFormat format)
    {
        var text = format switch
        {
            ReportFormat.Json => Json(report, sha256),
            ReportFormat.Html => Html(report, sha256),
            ReportFormat.Txt => PlainText(report, sha256),
            _ => Csv(report, sha256),
        };
        File.WriteAllText(path, text, format == ReportFormat.Csv ? WithBom : Plain);
    }

    private static string Csv(FileReport report, string? sha256)
    {
        var text = new StringBuilder();
        text.AppendLine("section,label,value");
        Row(text, "file", "path", report.Path);
        if (!string.IsNullOrEmpty(sha256)) Row(text, "file", "sha256", sha256);
        foreach (var alert in report.Alerts) Row(text, "alert", "alert", alert);
        foreach (var fact in report.Overview) Row(text, "overview", fact.Label, fact.Value);
        foreach (var section in report.Sections)
        {
            foreach (var fact in section.Rows) Row(text, section.Title, fact.Label, fact.Value);
        }
        return text.ToString();
    }

    private static void Row(StringBuilder text, string section, string label, string value)
    {
        text.Append(Quote(section)).Append(',')
            .Append(Quote(label)).Append(',')
            .AppendLine(Quote(value));
    }

    private static string Quote(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private static string Json(FileReport report, string? sha256)
    {
        var root = new Dictionary<string, object?>
        {
            ["path"] = report.Path,
            ["sha256"] = sha256,
            ["alerts"] = report.Alerts,
            ["overview"] = report.Overview.Select(fact => new Dictionary<string, string>
            {
                ["label"] = fact.Label,
                ["value"] = fact.Value,
            }),
            ["sections"] = report.Sections.Select(section => new Dictionary<string, object>
            {
                ["title"] = section.Title,
                ["rows"] = section.Rows.Select(fact => new Dictionary<string, string>
                {
                    ["label"] = fact.Label,
                    ["value"] = fact.Value,
                }).ToList(),
            }),
        };
        return System.Text.Json.JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string PlainText(FileReport report, string? sha256)
    {
        var text = new StringBuilder();
        text.AppendLine(report.Path);
        if (!string.IsNullOrEmpty(sha256)) text.AppendLine("SHA-256  " + sha256);
        foreach (var alert in report.Alerts) text.AppendLine(alert);
        text.AppendLine();
        foreach (var fact in report.Overview) Line(text, fact);
        foreach (var section in report.Sections)
        {
            text.AppendLine();
            text.AppendLine(section.Title);
            foreach (var fact in section.Rows) Line(text, fact);
        }
        return text.ToString();
    }

    private static void Line(StringBuilder text, FileFact fact) =>
        text.AppendLine($"{fact.Label}: {fact.Value.Replace("\n", "\n" + new string(' ', fact.Label.Length + 2))}");

    private static string Html(FileReport report, string? sha256)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        html.AppendLine("<title>Analyze</title>");
        html.AppendLine("<style>body{font:14px sans-serif;margin:24px}h2{font-size:15px;margin:24px 0 8px}");
        html.AppendLine("table{border-collapse:collapse;width:100%}td{padding:4px 10px 4px 0;vertical-align:top}");
        html.AppendLine(".alert{color:#b91c1c}</style></head><body>");
        html.Append("<h1>").Append(Escape(Path.GetFileName(report.Path))).AppendLine("</h1>");
        if (!string.IsNullOrEmpty(sha256))
        {
            html.Append("<p>SHA-256 ").Append(Escape(sha256)).AppendLine("</p>");
        }
        foreach (var alert in report.Alerts)
        {
            html.Append("<p class=\"alert\">").Append(Escape(alert)).AppendLine("</p>");
        }
        Table(html, report.Overview);
        foreach (var section in report.Sections)
        {
            html.Append("<h2>").Append(Escape(section.Title)).AppendLine("</h2>");
            Table(html, section.Rows);
        }
        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static void Table(StringBuilder html, IReadOnlyList<FileFact> rows)
    {
        html.AppendLine("<table>");
        foreach (var fact in rows)
        {
            html.Append("<tr><td>").Append(Escape(fact.Label)).Append("</td><td>")
                .Append(Escape(fact.Value).Replace("\n", "<br>")).AppendLine("</td></tr>");
        }
        html.AppendLine("</table>");
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
