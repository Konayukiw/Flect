using Optimizer.Main;
using Optimizer.Gui;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace Optimizer.Tasks;

internal sealed class PdfPreview(TaskRequest request) : TaskBase(request)
{
    public override string Title => Loc.T("menu.pdf.preview");

    public override bool ShowsProgress => false;

    public override Task RunAsync(ITaskProgress progress) => Task.CompletedTask;

    public override void Present() => new Gui.PdfPreview(Request.Paths[0]).Show();
}

internal sealed class PdfMerge(TaskRequest request) : TaskBase(request)
{
    private string? _output;

    public override string Title => Loc.T("menu.pdf.merge");

    public override string? Summary => _output is null
        ? null
        : Loc.F("msg.mergedInto", Loc.N("count.document", Request.Paths.Count),
                Path.GetFileName(_output));

    public override Task RunAsync(ITaskProgress progress) => Task.Run(() =>
    {
        progress.Indeterminate();
        progress.Status(Loc.T("msg.merging"));

        var sources = Request.Paths
            .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        progress.Token.ThrowIfCancellationRequested();
        var merged = PdfMerger.Merge(sources);

        var output = OutputPath.Derive(sources[0], "_merged", ".pdf");
        using var working = new WorkingFile(output);
        File.WriteAllBytes(output, merged);
        working.Keep();

        _output = output;
    }, progress.Token);
}

internal sealed class PdfConvert(TaskRequest request) : BatchTask(request)
{
    private string _target = "PNG";

    public override string Title => Loc.T("menu.pdf.convert");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "PNG").ToUpperInvariant();
        return true;
    }

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        if (_target == "TXT")
        {
            ExtractText(path);
            return;
        }
        RenderPages(path, progress);
    }, progress.Token);

    private static void ExtractText(string path)
    {
        var output = OutputPath.Derive(path, string.Empty, ".txt");
        using var working = new WorkingFile(output);
        using var document = PdfDocument.Open(path);

        using (var writer = new StreamWriter(output, append: false,
                                             new System.Text.UTF8Encoding(false)))
        {
            foreach (var page in document.GetPages())
            {
                writer.WriteLine(ContentOrderTextExtractor.GetText(page));
                writer.WriteLine();
            }
        }
        working.Keep();
    }

    private void RenderPages(string path, ITaskProgress progress)
    {
        var (format, extension, quality) = _target == "JPG"
            ? (SKEncodedImageFormat.Jpeg, ".jpg", 90)
            : (SKEncodedImageFormat.Png, ".png", 100);

        int pages = PdfRenderer.PageCount(path);
        if (pages == 0) throw new InvalidOperationException(Loc.T("msg.noPages"));

        int width = pages.ToString().Length;
        var name = Path.GetFileName(path);

        for (int index = 0; index < pages; index++)
        {
            progress.Token.ThrowIfCancellationRequested();
            progress.Status(Loc.F("msg.pageOf", name, index + 1, pages));

            var suffix = pages == 1 ? string.Empty : $"_p{(index + 1).ToString().PadLeft(width, '0')}";
            var output = OutputPath.Derive(path, suffix, extension);

            using var working = new WorkingFile(output);
            File.WriteAllBytes(output, PdfRenderer.Render(path, index, format, quality));
            working.Keep();
        }
    }
}
