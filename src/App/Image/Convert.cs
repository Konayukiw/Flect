using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal sealed class Convert(Request request) : ImageBatch(request)
{
    private const uint IconMaxEdge = 256;

    private string _target = "PNG";

    public override string Title => Loc.T("menu.image.convert");
    protected override string Suffix => string.Empty;
    protected override string? OutputExtension => "." + _target.ToLowerInvariant();

    public override bool Configure()
    {
        _target = (Request["to"] ?? "PNG").ToUpperInvariant();
        return true;
    }

    protected override void Transform(IMagickImage<byte> frame)
    {
        switch (_target)
        {
            case "ICO":
                if (frame.Width > IconMaxEdge || frame.Height > IconMaxEdge)
                {
                    frame.Resize(new MagickGeometry($"{IconMaxEdge}x{IconMaxEdge}"));
                }
                break;

            case "JPG":
                frame.BackgroundColor = MagickColors.White;
                frame.Alpha(AlphaOption.Remove);
                break;

            case "WEBP":
                frame.Quality = (uint)Math.Clamp(Preferences.WebpQuality, 1, 100);
                break;
        }
    }
}
