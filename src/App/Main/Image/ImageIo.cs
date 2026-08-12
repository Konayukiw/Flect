using System.Windows.Media.Imaging;
using ImageMagick;

namespace Optimizer.Main.Image;

internal static class ImageIo
{
    private static readonly Guid HeifContainerFormat = new("E1E62521-6787-405B-A339-500715B5763F");

    private const string EncoderMissing =
        "Windows has no HEIC encoder available on this machine. Install \"HEVC Video " +
        "Extensions\" from the Microsoft Store to write .heic files.";

    public static bool IsHeif(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".heic" or ".heif";

    public static bool IsHeif(MagickFormat format) =>
        format is MagickFormat.Heic or MagickFormat.Heif;

    public static void Write(IMagickImage<byte> image, string path)
    {
        if (!IsHeif(path))
        {
            image.Write(path);
            return;
        }
        File.WriteAllBytes(path, EncodeHeif(image));
    }

    public static byte[] EncodeHeif(IMagickImage<byte> image)
    {
        var png = image.ToByteArray(MagickFormat.Png32);

        BitmapSource source;
        using (var input = new MemoryStream(png))
        {
            source = BitmapFrame.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        source.Freeze();

        using var output = new MemoryStream();
        try
        {
            var encoder = BitmapEncoder.Create(HeifContainerFormat);
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(output);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new NotSupportedException(EncoderMissing, ex);
        }

        if (output.Length == 0) throw new NotSupportedException(EncoderMissing);
        return output.ToArray();
    }
}
