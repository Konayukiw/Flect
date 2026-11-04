using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal abstract class ImageBatch(Request request) : Flect.Dispatch.Batch(request)
{
    protected static ImageSettings Preferences => global::Flect.Settings.Settings.Current.Image;

    protected abstract string Suffix { get; }
    protected virtual string? OutputExtension => null;

    protected abstract void Transform(IMagickImage<byte> frame);

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var output = Flect.Files.OutputName.Derive(path, Suffix, OutputExtension);
        using var working = new Flect.Files.TempFile(output);
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
            ImageFile.WriteSvg(chosen, output);
        }
        else
        {
            ImageFile.Write(chosen, output);
        }
        working.Keep();
    }, progress.Token);

    protected static bool SupportsAlpha(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe or MagickFormat.Bmp => false,
        _ => true,
    };
}
