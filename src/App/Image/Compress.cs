using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal sealed class Compress(Request request) : Flect.Dispatch.Batch(request)
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
                                Flect.Display.Format.Bytes(original)));
            return;
        }

        using var image = new MagickImage(path);
        var attempt = Squeeze(image, _target, progress.Token);

        var output = Flect.Files.OutputName.Derive(path, "_compressed", attempt.AsWebp ? ".webp" : null);
        using (var working = new Flect.Files.TempFile(output))
        {
            File.WriteAllBytes(output, attempt.Data);
            working.Keep();
        }

        if (attempt.AsWebp)
        {
            progress.Info(Loc.F("msg.webpFallback", Path.GetFileName(path),
                                Flect.Display.Format.Bytes(_target)));
        }
        if (!attempt.Reached)
        {
            progress.Warn(Loc.F("msg.targetMissed", Path.GetFileName(path),
                                Flect.Display.Format.Bytes(_target),
                                Flect.Display.Format.Bytes(attempt.Data.LongLength)));
        }
    }, progress.Token);

    private readonly record struct Attempt(byte[] Data, bool Reached, bool AsWebp);

    private static Attempt Squeeze(MagickImage image, long target, CancellationToken token)
    {
        using var native = (MagickImage)image.Clone();
        var (bytes, reached) = Shrink(native, target, token);

        if (reached) return new Attempt(bytes, true, false);
        if (!Preferences.Current.Image.AllowWebpFallback || image.Format == MagickFormat.WebP)
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
            ImageFile.IsHeif(image.Format) ? _ => ImageFile.EncodeHeif(image)
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
