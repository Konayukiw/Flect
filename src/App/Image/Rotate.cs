using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal sealed class Rotate(Request request) : ImageBatch(request)
{
    private double _degrees = 90;

    public override string Title => Loc.T("menu.image.rotate");
    protected override string Suffix => "_rotated";

    public override bool Configure()
    {
        if (double.TryParse(Request["deg"], NumberStyles.Float, CultureInfo.InvariantCulture,
                            out var value) && double.IsFinite(value))
        {
            _degrees = value;
        }
        return true;
    }

    protected override void Transform(IMagickImage<byte> frame)
    {
        frame.BackgroundColor = SupportsAlpha(frame.Format)
            ? MagickColors.Transparent
            : MagickColors.White;
        frame.Rotate(_degrees);
    }
}
