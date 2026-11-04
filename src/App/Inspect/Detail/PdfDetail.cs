using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Outline;

namespace Flect.Inspect.Detail;

internal static class PdfDetail
{
    public static ProbeResult Read(string path)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            var bag = new FactBag();
            bag.Add("dialog.fileAnalyze.version", document.Version.ToString());
            bag.Add("dialog.fileAnalyze.pages", document.NumberOfPages.ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.pageSize", PageSizes(document));
            bag.Add("dialog.fileAnalyze.encrypted", Kinds.YesNo(document.IsEncrypted));
            if (!document.IsEncrypted)
            {
                bag.Add("dialog.fileAnalyze.print", Kinds.YesNo(true));
                bag.Add("dialog.fileAnalyze.copy", Kinds.YesNo(true));
            }

            var info = document.Information;
            bag.Add("dialog.fileAnalyze.metaTitle", info.Title);
            bag.Add("dialog.fileAnalyze.author", info.Author);
            bag.Add("dialog.fileAnalyze.subject", info.Subject);
            bag.Add("dialog.fileAnalyze.keywords", info.Keywords);
            bag.Add("dialog.fileAnalyze.creator", info.Creator);
            bag.Add("dialog.fileAnalyze.producer", info.Producer);
            bag.Add("dialog.fileAnalyze.pdfCreated", info.CreationDate);
            bag.Add("dialog.fileAnalyze.pdfModified", info.ModifiedDate);
            bag.Add("dialog.fileAnalyze.bookmarks",
                    Count(document).ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.embedded", Embedded(document));

            var section = bag.Section("dialog.fileAnalyze.pdf");
            return new ProbeResult
            {
                Kind = "PDF",
                Sections = section is null ? [] : [section],
            };
        }
        catch (PdfDocumentEncryptedException)
        {
            var bag = new FactBag();
            bag.Add("dialog.fileAnalyze.encrypted", Kinds.YesNo(true));
            bag.Add("dialog.fileAnalyze.password", Loc.T("dialog.fileAnalyze.passwordBody"));
            var section = bag.Section("dialog.fileAnalyze.pdf");
            return new ProbeResult
            {
                Kind = "PDF",
                Sections = section is null ? [] : [section],
            };
        }
    }

    private static int Count(PdfDocument document)
    {
        if (!document.TryGetBookmarks(out var bookmarks)) return 0;
        return bookmarks.Roots.Sum(CountNode);
    }

    private static int CountNode(BookmarkNode node)
    {
        var children = node.Children?.Sum(CountNode) ?? 0;
        return 1 + children;
    }

    private static string Embedded(PdfDocument document)
    {
        if (!document.Advanced.TryGetEmbeddedFiles(out var files) || files.Count == 0)
        {
            return Loc.T("dialog.fileAnalyze.none");
        }

        var names = files.Select(file => file.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        if (names.Count == 0) return files.Count.ToString(CultureInfo.InvariantCulture);
        if (names.Count <= 30) return string.Join(", ", names);
        return string.Join(", ", names.Take(30)) + " +" + (names.Count - 30).ToString(CultureInfo.InvariantCulture);
    }

    private static string? PageSizes(PdfDocument document)
    {
        if (document.NumberOfPages == 0) return null;
        var groups = new List<(string Label, int Count)>();
        foreach (var page in document.GetPages())
        {
            var sizeName = page.Size.ToString();
            var label = sizeName is "Custom" or "Unknown"
                ? Millimetres(page.Width, page.Height)
                : $"{sizeName} ({Millimetres(page.Width, page.Height)})";
            var index = groups.FindIndex(group => group.Label == label);
            if (index < 0) groups.Add((label, 1));
            else groups[index] = (label, groups[index].Count + 1);
        }

        if (groups.Count == 1) return groups[0].Label;
        return string.Join(", ", groups.Select(group => $"{group.Label} x {group.Count}"));
    }

    private static string Millimetres(double width, double height)
    {
        var wide = width / 72d * 25.4d;
        var high = height / 72d * 25.4d;
        return string.Create(CultureInfo.InvariantCulture, $"{wide:0.#} x {high:0.#} mm");
    }
}
