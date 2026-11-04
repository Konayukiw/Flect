using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace Flect.Pdf;

internal sealed class Preview(Request request) : TaskBase(request)
{
    public override string Title => Loc.T("menu.pdf.preview");

    public override bool ShowsProgress => false;

    public override Task RunAsync(ITaskProgress progress) => Task.CompletedTask;

    public override void Present() => new PreviewWindow(Request.Paths[0]).Show();
}
