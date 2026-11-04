using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal sealed class Metadata(Request request) : ImageBatch(request)
{
    public override string Title => Loc.T("menu.image.strip");
    protected override string Suffix => "_clean";

    protected override void Transform(IMagickImage<byte> frame)
    {
        var metadata = Preferences.Metadata;

        if (metadata.ApplyOrientation) frame.AutoOrient();

        if (metadata.RemoveExif)
        {
            frame.RemoveProfile("exif");
        }
        else if (metadata.RemoveGps)
        {
            StripGps(frame);
        }

        if (metadata.RemoveComments)
        {
            frame.RemoveProfile("xmp");
            frame.RemoveProfile("iptc");
            frame.RemoveAttribute("comment");
            frame.RemoveAttribute("Software");
        }

        if (metadata.RemoveColorProfile)
        {
            frame.RemoveProfile("icc");
            frame.RemoveProfile("icm");
        }
    }

    private static void StripGps(IMagickImage<byte> frame)
    {
        var exif = frame.GetExifProfile();
        if (exif is null) return;

        var located = exif.Values
            .Where(value => value.Tag.ToString()?.StartsWith("GPS", StringComparison.Ordinal) == true)
            .Select(value => value.Tag)
            .ToList();
        if (located.Count == 0) return;

        foreach (var tag in located) exif.RemoveValue(tag);
        frame.SetProfile(exif);
    }
}
