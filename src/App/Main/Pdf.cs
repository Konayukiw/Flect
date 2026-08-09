using PDFtoImage;
using SkiaSharp;

namespace Optimizer.Main;

internal static class PdfRenderer
{
    public const int DefaultDpi = 150;

    public static int PageCount(string path)
    {
        using var stream = File.OpenRead(path);
        return Conversion.GetPageCount(stream);
    }

    public static byte[] Render(string path, int pageIndex, SKEncodedImageFormat format,
                                int quality = 90, int dpi = DefaultDpi)
    {
        using var stream = File.OpenRead(path);
        using var bitmap = Conversion.ToImage(stream, page: pageIndex,
                                              options: new RenderOptions(Dpi: dpi));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }
}
