using System.Globalization;
using System.Text.Json;

namespace Flect.Video;

internal sealed record MediaInfo(
    double DurationSeconds,
    int Width,
    int Height,
    double FrameRate,
    string? VideoCodec,
    string? AudioCodec,
    long VideoBitrate,
    long AudioBitrate,
    int AudioChannels,
    int AudioTrackCount,
    int SubtitleTrackCount,
    string Container)
{
    public bool HasVideo => VideoCodec is not null;
    public bool HasAudio => AudioTrackCount > 0;
    public int ShortSide => Math.Min(Width, Height);

    public bool IsMp4Family =>
        Container.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
        Container.Contains("mov", StringComparison.OrdinalIgnoreCase) ||
        Container.Contains("m4a", StringComparison.OrdinalIgnoreCase);
}
