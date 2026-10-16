namespace Optimizer.Main.Media;

internal static class Containers
{
    public static VideoContainer Resolve(VideoSettings video) => video.Container switch
    {
        VideoContainer.Auto => video.Codec == VideoCodecChoice.Vp9 ? VideoContainer.WebM : VideoContainer.Mp4,
        var chosen => chosen,
    };

    public static bool SuitsWebM(VideoCodecChoice codec) =>
        codec is VideoCodecChoice.Vp9 or VideoCodecChoice.Av1;

    public static bool SuitsMov(VideoCodecChoice codec) =>
        codec is VideoCodecChoice.H264 or VideoCodecChoice.H265;

    public static VideoCodecChoice CodecFor(VideoContainer container, VideoCodecChoice codec) =>
        container == VideoContainer.WebM && !SuitsWebM(codec) ? VideoCodecChoice.Vp9 : codec;

    public static string Extension(VideoContainer container) => container switch
    {
        VideoContainer.Mkv => ".mkv",
        VideoContainer.WebM => ".webm",
        _ => ".mp4",
    };

    public static bool CanCopyVideo(MediaInfo source, VideoContainer container) =>
        container == VideoContainer.WebM
            ? source.VideoCodec is "vp8" or "vp9" or "av1"
            : string.Equals(source.VideoCodec, "h264", StringComparison.OrdinalIgnoreCase);

    public static bool CanCopyAudio(MediaInfo source, VideoContainer container) =>
        source.AudioCodec is not null &&
        (container == VideoContainer.WebM
            ? source.AudioCodec is "opus" or "vorbis"
            : source.AudioCodec.StartsWith("aac", StringComparison.OrdinalIgnoreCase));

    public static bool AlreadyIn(MediaInfo source, VideoContainer container) =>
        container == VideoContainer.Mp4
            ? source.IsMp4Family
            : source.Container.Contains("matroska", StringComparison.OrdinalIgnoreCase);
}
