using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace Flect.Pdf;

internal sealed class Merge(Request request) : TaskBase(request)
{
    private string[] _sources = [];
    private string? _output;

    public override string Title => Loc.T("menu.pdf.merge");

    public override string? Summary => _output is null
        ? null
        : Loc.F("msg.mergedInto", Loc.N("count.document", _sources.Length),
                Path.GetFileName(_output));

    public override bool Configure()
    {
        var sorted = Request.Paths.OrderBy(Path.GetFileName, Flect.Files.ExplorerSort.Instance).ToList();
        var chosen = Sequence.Ask(sorted);
        if (chosen is null) return false;

        _sources = [.. chosen];
        return true;
    }

    public override Task RunAsync(ITaskProgress progress) => Task.Run(() =>
    {
        progress.Indeterminate();
        progress.Status(Loc.T("msg.merging"));

        progress.Token.ThrowIfCancellationRequested();
        var merged = PdfMerger.Merge(_sources);

        var output = Flect.Files.OutputName.Derive(_sources[0], "_merged", ".pdf");
        using var working = new Flect.Files.TempFile(output);
        File.WriteAllBytes(output, merged);
        working.Keep();

        _output = output;
    }, progress.Token);
}
