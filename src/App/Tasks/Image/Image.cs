using ImageMagick;
using Optimizer.Main.Process;

namespace Optimizer.Tasks.Image;

internal abstract class ImageTask(Request request) : BatchTask(request)
{
    protected static ImageSettings Preferences => Settings.Current.Image;

    protected abstract string Suffix { get; }
    protected virtual string? OutputExtension => null;

    protected abstract void Transform(IMagickImage<byte> frame);

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var output = OutputPath.Derive(path, Suffix, OutputExtension);
        using var working = new WorkingFile(output);
        using var frames = new MagickImageCollection(path);

        if (frames.Count > 1 && frames[0].Format == MagickFormat.Gif
            && !string.Equals(OutputExtension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            frames.Coalesce();
            foreach (var frame in frames) Transform(frame);
            frames.Optimize();
            frames.Write(output);
            working.Keep();
            return;
        }

        var chosen = frames.OrderByDescending(frame => (long)frame.Width * frame.Height).First();
        Transform(chosen);
        if (string.Equals(OutputExtension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            ImageIo.WriteSvg(chosen, output);
        }
        else
        {
            ImageIo.Write(chosen, output);
        }
        working.Keep();
    }, progress.Token);

    protected static bool SupportsAlpha(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe or MagickFormat.Bmp => false,
        _ => true,
    };
}

internal sealed class ImageResize(Request request) : ImageTask(request)
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
            if (double.TryParse(scale, out var value) && value > 0) _percent = value;
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

internal sealed class ImageRotate(Request request) : ImageTask(request)
{
    private double _degrees = 90;

    public override string Title => Loc.T("menu.image.rotate");
    protected override string Suffix => "_rotated";

    public override bool Configure()
    {
        if (double.TryParse(Request["deg"], out var value)) _degrees = value;
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

internal sealed class ImageConvert(Request request) : ImageTask(request)
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

internal sealed class ImageStripMetadata(Request request) : ImageTask(request)
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

internal sealed class ImageChromaKey(Request request) : ImageTask(request)
{
    public override string Title => Loc.T("menu.image.chromakey");

    protected override string Suffix => "_keyed";

    protected override string? OutputExtension => ".png";

    protected override void Transform(IMagickImage<byte> frame)
    {
        var image = Preferences;
        var key = ColorText.Parse(image.BackgroundKeyColor, System.Windows.Media.Colors.Lime);

        frame.ColorFuzz = new Percentage(Math.Clamp(image.BackgroundTolerance, 0, 100));
        frame.Transparent(new MagickColor(key.R, key.G, key.B));
        frame.ColorFuzz = new Percentage(0);
    }
}

internal sealed class ImageCompress(Request request) : BatchTask(request)
{
    private const int MaxDownscaleRounds = 8;
    private const double DownscaleFactor = 0.8;
    private const uint MinimumEdge = 32;

    private long _target;

    public override string Title => Loc.T("menu.image.compress");

    public override bool Configure()
    {
        var bytes = Request["bytes"];
        if (string.Equals(bytes, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var answer = TargetBytes.Ask(LargestSelected());
            if (answer is null) return false;
            _target = answer.Value;
            return true;
        }
        return long.TryParse(bytes, out _target) && _target > 0;
    }

    private long LargestSelected()
    {
        long largest = 0;
        foreach (var path in Request.Paths)
        {
            try { largest = Math.Max(largest, new FileInfo(path).Length); }
            catch (IOException) {}
        }
        return largest;
    }

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var original = new FileInfo(path).Length;
        if (original <= _target)
        {
            progress.Info(Loc.F("msg.alreadySmall", Path.GetFileName(path),
                                Formatting.Bytes(original)));
            return;
        }

        using var image = new MagickImage(path);
        var attempt = Squeeze(image, _target, progress.Token);

        var output = OutputPath.Derive(path, "_compressed", attempt.AsWebp ? ".webp" : null);
        using (var working = new WorkingFile(output))
        {
            File.WriteAllBytes(output, attempt.Data);
            working.Keep();
        }

        if (attempt.AsWebp)
        {
            progress.Info(Loc.F("msg.webpFallback", Path.GetFileName(path),
                                Formatting.Bytes(_target)));
        }
        if (!attempt.Reached)
        {
            progress.Warn(Loc.F("msg.targetMissed", Path.GetFileName(path),
                                Formatting.Bytes(_target),
                                Formatting.Bytes(attempt.Data.LongLength)));
        }
    }, progress.Token);

    private readonly record struct Attempt(byte[] Data, bool Reached, bool AsWebp);

    private static Attempt Squeeze(MagickImage image, long target, CancellationToken token)
    {
        using var native = (MagickImage)image.Clone();
        var (bytes, reached) = Shrink(native, target, token);

        if (reached) return new Attempt(bytes, true, false);
        if (!Settings.Current.Image.AllowWebpFallback || image.Format == MagickFormat.WebP)
        {
            return new Attempt(bytes, false, false);
        }

        using var alternative = (MagickImage)image.Clone();
        alternative.Format = MagickFormat.WebP;
        var (webp, webpReached) = Shrink(alternative, target, token);

        return webpReached || webp.LongLength < bytes.LongLength
            ? new Attempt(webp, webpReached, true)
            : new Attempt(bytes, false, false);
    }

    private static (byte[] Data, bool Reached) Shrink(MagickImage image, long target,
                                                      CancellationToken token)
    {
        Func<long, byte[]> shrink =
            ImageIo.IsHeif(image.Format) ? _ => ImageIo.EncodeHeif(image)
            : IsLossy(image.Format) ? bytes => HighestQualityUnder(image, bytes)
            : bytes => SmallestPalette(image, bytes);

        var best = shrink(target);
        if (best.LongLength <= target) return (best, true);

        for (int round = 0; round < MaxDownscaleRounds; round++)
        {
            token.ThrowIfCancellationRequested();

            if (image.Width <= MinimumEdge || image.Height <= MinimumEdge) break;
            image.Resize(new MagickGeometry(
                $"{(uint)(image.Width * DownscaleFactor)}x{(uint)(image.Height * DownscaleFactor)}"));

            var candidate = shrink(target);
            if (candidate.LongLength < best.LongLength) best = candidate;
            if (best.LongLength <= target) return (best, true);
        }

        return (best, best.LongLength <= target);
    }

    private static byte[] HighestQualityUnder(MagickImage image, long target)
    {
        byte[]? fitting = null;
        byte[]? smallest = null;
        uint low = 5;
        uint high = 95;

        while (low <= high)
        {
            uint mid = (low + high) / 2;
            image.Quality = mid;
            var data = image.ToByteArray();

            if (smallest is null || data.Length < smallest.Length) smallest = data;

            if (data.LongLength <= target)
            {
                fitting = data;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return fitting ?? smallest!;
    }

    private static byte[] SmallestPalette(MagickImage image, long target)
    {
        byte[]? best = null;
        foreach (uint colors in (uint[])[256, 128, 64, 32])
        {
            using var clone = (MagickImage)image.Clone();
            clone.Quantize(new QuantizeSettings { Colors = colors });
            var data = clone.ToByteArray();

            if (best is null || data.Length < best.Length) best = data;
            if (data.LongLength <= target) return data;
        }
        return best!;
    }

    private static bool IsLossy(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe => true,
        MagickFormat.WebP => true,
        _ => false,
    };
}
