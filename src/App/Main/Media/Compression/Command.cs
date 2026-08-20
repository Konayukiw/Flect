using System.Globalization;

namespace Optimizer.Main.Media.Compression;

internal static class Command
{
    private static readonly string[] TrackMaps = ["-map", "0:v:0", "-map", "0:a:0?"];

    private static readonly string[] FastStart = ["-movflags", "+faststart"];

    public static string Extension() => Settings.Current.Video.Container switch
    {
        VideoContainer.Mkv => ".mkv",
        VideoContainer.WebM => ".webm",
        _ => ".mp4",
    };

    private static string[] ContainerFlags()
    {
        var container = Settings.Current.Video.Container;
        return container is VideoContainer.Mkv or VideoContainer.WebM ? [] : FastStart;
    }

    public static string[] Remux(string input, string output) =>
    [
        .. Ffmpeg.Preamble, "-i", input,
        .. TrackMaps,
        "-c", "copy",
        .. ContainerFlags(),
        output,
    ];

    public static string[] AudioOnly(string input, string output, Plan plan) =>
    [
        .. Ffmpeg.Preamble, "-i", input,
        .. TrackMaps,
        "-c:v", "copy",
        .. AudioCodec(plan),
        .. ContainerFlags(),
        output,
    ];

    public static EncoderChoice Encoder()
    {
        var video = Settings.Current.Video;
        return Encoders.Preferred(video.Codec, video.Hardware);
    }

    public static string[] SinglePass(string input, string output, Plan plan,
                                      EncoderChoice encoder, int videoKbps, int threads)
    {
        var video = Settings.Current.Video;

        return
        [
            .. Ffmpeg.Preamble, "-i", input,
            .. TrackMaps,
            .. VideoFilter(plan),
            "-threads", threads.ToString(CultureInfo.InvariantCulture),
            .. Encoders.Bitrate(encoder, videoKbps, video.Priority),
            .. Encoders.PixelFormat(encoder),
            .. AudioCodec(plan),
            .. ContainerFlags(),
            output,
        ];
    }

    public static string[] AudioOnlySource(string input, string output, int audioKbps) =>
    [
        .. Ffmpeg.Preamble, "-i", input,
        "-vn",
        "-c:a", "aac", "-b:a", $"{audioKbps}k",
        .. ContainerFlags(),
        output,
    ];

    private static string[] AudioCodec(Plan plan) => plan.Audio.Mode switch
    {
        AudioMode.None => ["-an"],
        AudioMode.Copy => ["-c:a", "copy"],
        _ => ["-c:a", "aac", "-b:a", $"{plan.Audio.EncodeKbps}k"],
    };

    private static string[] VideoFilter(Plan plan)
    {
        var profile = plan.Profile;
        int sourceWidth = Profile.MakeEven(plan.Source.Width);
        int sourceHeight = Profile.MakeEven(plan.Source.Height);
        double sourceFps = plan.Source.FrameRate > 0 ? plan.Source.FrameRate : 30;

        var parts = new List<string>();
        if (profile.Width != sourceWidth || profile.Height != sourceHeight)
        {
            parts.Add($"scale={profile.Width}:{profile.Height}");
        }
        if (Math.Abs(profile.Fps - sourceFps) > 0.01)
        {
            parts.Add("fps=" + profile.Fps.ToString("0.###", CultureInfo.InvariantCulture));
        }

        return parts.Count > 0 ? ["-vf", string.Join(',', parts)] : [];
    }
}
