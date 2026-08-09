using ImageMagick;
using Optimizer.Main;
using Optimizer.Gui;

namespace Optimizer.Tasks;

internal abstract class ImageTask(TaskRequest request) : BatchTask(request)
{
    protected abstract string Suffix { get; }
    protected virtual string? OutputExtension => null;

    protected abstract void Transform(IMagickImage<byte> frame);

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var output = OutputPath.Derive(path, Suffix, OutputExtension);
        using var working = new WorkingFile(output);
        using var frames = new MagickImageCollection(path);

        if (frames.Count > 1 && frames[0].Format == MagickFormat.Gif)
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
        ImageIo.Write(chosen, output);
        working.Keep();
    }, progress.Token);

    protected static bool SupportsAlpha(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe or MagickFormat.Bmp => false,
        _ => true,
    };
}

internal sealed class ImageResize(TaskRequest request) : ImageTask(request)
{
    private PixelTarget? _pixels;
    private double _percent = 50;

    public override string Title => "Resize";
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

internal sealed class ImageRotate(TaskRequest request) : ImageTask(request)
{
    private double _degrees = 90;

    public override string Title => "Rotate";
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

internal sealed class ImageConvert(TaskRequest request) : ImageTask(request)
{
    private const uint IconMaxEdge = 256;

    private string _target = "PNG";

    public override string Title => "Convert";
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
        }
    }
}

internal sealed class ImageStripMetadata(TaskRequest request) : ImageTask(request)
{
    public override string Title => "Remove Metadata";
    protected override string Suffix => "_clean";

    protected override void Transform(IMagickImage<byte> frame)
    {
        frame.AutoOrient();

        var colorProfile = frame.GetColorProfile();
        frame.Strip();
        if (colorProfile is not null) frame.SetProfile(colorProfile);
    }
}

internal sealed class ImageChromaKey(TaskRequest request) : ImageTask(request)
{
    private static readonly Percentage Tolerance = new(25);

    private static readonly MagickColor Key = MagickColors.Lime;

    public override string Title => "Remove Green Screen";
    
    protected override string Suffix => "_keyed";

    protected override string? OutputExtension => ".png";

    protected override void Transform(IMagickImage<byte> frame)
    {
        frame.ColorFuzz = Tolerance;
        frame.Transparent(Key);
        frame.ColorFuzz = new Percentage(0);
    }
}

internal sealed class ImageCompress(TaskRequest request) : BatchTask(request)
{
    private const int MaxDownscaleRounds = 8;
    private const double DownscaleFactor = 0.8;
    private const uint MinimumEdge = 32;

    private long _target;

    public override string Title => "Compress";

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
            progress.Info(
                $"{Path.GetFileName(path)} is already {Formatting.Bytes(original)} — left alone.");
            return;
        }

        using var image = new MagickImage(path);
        var (data, reached) = Squeeze(image, _target, progress.Token);

        var output = OutputPath.Derive(path, "_compressed");
        using (var working = new WorkingFile(output))
        {
            File.WriteAllBytes(output, data);
            working.Keep();
        }

        if (!reached)
        {
            progress.Warn($"{Path.GetFileName(path)} — {Formatting.Bytes(_target)} was not " +
                          $"reachable, smallest was {Formatting.Bytes(data.LongLength)}.");
        }
    }, progress.Token);

    private static (byte[] Data, bool Reached) Squeeze(MagickImage image, long target,
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
