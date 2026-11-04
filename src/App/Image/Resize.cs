using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal sealed class Resize(Request request) : ImageBatch(request)
{
    private PixelTarget? _pixels;
    private double _percent = 50;

    public override string Title => Loc.T("menu.image.resize");
    protected override string Suffix => "_resized";

    public override bool Configure()
    {
        var scale = Request["scale"];
        if (!string.Equals(scale, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(scale, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                value > 0)
            {
                _percent = value;
            }
            return true;
        }
        _pixels = Pixels.Ask();
        return _pixels is not null;
    }

    protected override void Transform(IMagickImage<byte> frame)
    {
        if (_pixels is null)
        {
            frame.Resize(new Percentage(_percent));
            return;
        }
        frame.Resize(new MagickGeometry(Geometry(_pixels)));
    }

    private static string Geometry(PixelTarget target)
    {
        if (target.Width > 0 && target.Height > 0)
        {
            return $"{target.Width}x{target.Height}" + (target.KeepAspect ? string.Empty : "!");
        }
        return target.Width > 0 ? target.Width.ToString() : $"x{target.Height}";
    }
}
