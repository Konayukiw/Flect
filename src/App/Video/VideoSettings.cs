namespace Flect.Video;

internal sealed class VideoSettings
{
    public VideoPresets Presets { get; set; } = new();
    public TrimSettings Trim { get; set; } = new();
    public ThumbnailSettings Thumbnail { get; set; } = new();
    public VideoCodecChoice Codec { get; set; } = VideoCodecChoice.H264;
    public VideoContainer Container { get; set; } = VideoContainer.Auto;
    public HardwareEncoder Hardware { get; set; } = HardwareEncoder.None;
    public EncodePriority Priority { get; set; } = EncodePriority.Balanced;

    public int GifFps { get; set; } = 15;
    public int GifMaxWidth { get; set; } = 640;
    public GifDither GifDither { get; set; } = GifDither.Bayer;

    public string RotateFillColor { get; set; } = "#000000";

    public SubtitlesSettings Subtitles { get; set; } = new();
}
