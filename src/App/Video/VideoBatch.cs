using System.Globalization;

namespace Flect.Video;

internal abstract class VideoBatch(Request request) : Flect.Dispatch.Batch(request)
{
    protected static VideoSettings Preferences => global::Flect.Settings.Settings.Current.Video;

    protected static string[] VideoEncoder(EncoderChoice encoder) =>
    [
        .. Encoder.Quality(encoder, Preferences.Priority),
        .. Encoder.PixelFormat(encoder),
    ];

    protected static string[] AudioCodec(string extension) => extension switch
    {
        ".webm" => ["-c:a", "libopus"],
        ".avi" => ["-c:a", "libmp3lame"],
        _ => ["-c:a", "aac"],
    };

    protected static string[] AudioEncoder(string extension) =>
        [.. AudioCodec(extension), "-b:a", "192k"];

    protected static async Task FilterAsync(string input, string output, string filter,
                                            MediaInfo info, ITaskProgress progress, string label)
    {
        var extension = Path.GetExtension(output).ToLowerInvariant();

        string[] Build(string[] audio, EncoderChoice encoder, int threads) =>
            [.. Ffmpeg.Preamble, "-i", input, "-vf", filter,
             "-threads", threads.ToString(CultureInfo.InvariantCulture),
             .. VideoEncoder(encoder), .. audio, output];

        using var working = new Flect.Files.TempFile(output);
        try
        {
            await Ffmpeg.RunEncodeAsync(Encoder.ForContainer(extension, Preferences),
                                        (encoder, threads) => Build(["-c:a", "copy"], encoder, threads),
                                        progress, label, info.DurationSeconds, info.ShortSide);
        }
        catch (InvalidOperationException)
        {
            Flect.Files.OutputName.SafeDelete(output);
            await Ffmpeg.RunEncodeAsync(Encoder.ForContainer(extension, Preferences),
                                        (encoder, threads) =>
                                            Build(AudioEncoder(extension), encoder, threads),
                                        progress, label, info.DurationSeconds, info.ShortSide);
        }
        working.Keep();
    }

    protected static void RequireVideo(MediaInfo info)
    {
        if (!info.HasVideo) throw new InvalidOperationException(Loc.T("msg.noVideoStream"));
    }

    protected long LargestSelected()
    {
        long largest = 0;
        foreach (var path in Request.Paths)
        {
            try { largest = Math.Max(largest, new FileInfo(path).Length); }
            catch (IOException) {}
        }
        return largest;
    }
}
