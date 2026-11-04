using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace Flect.Pdf;

internal sealed class Convert(Request request) : Flect.Dispatch.Batch(request)
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
        var output = Flect.Files.OutputName.Derive(path, string.Empty, ".txt");
        using var working = new Flect.Files.TempFile(output);
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

        int pages = Raster.PageCount(path);
        if (pages == 0) throw new InvalidOperationException(Loc.T("msg.noPages"));

        int width = pages.ToString().Length;
        var name = Path.GetFileName(path);

        for (int index = 0; index < pages; index++)
        {
            progress.Token.ThrowIfCancellationRequested();
            progress.Status(Loc.F("msg.pageOf", name, index + 1, pages));

            var suffix = pages == 1 ? string.Empty : $"_p{(index + 1).ToString().PadLeft(width, '0')}";
            var output = Flect.Files.OutputName.Derive(path, suffix, extension);

            using var working = new Flect.Files.TempFile(output);
            File.WriteAllBytes(output, Raster.Render(path, index, format, quality));
            working.Keep();
        }
    }
}
