using System.Globalization;
using System.Text.Json;

namespace Optimizer.Main;

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
    public bool VideoCopyable => string.Equals(VideoCodec, "h264", StringComparison.OrdinalIgnoreCase);
    public bool AudioCopyable =>
        AudioCodec is not null && AudioCodec.StartsWith("aac", StringComparison.OrdinalIgnoreCase);

    public bool IsMp4Family =>
        Container.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
        Container.Contains("mov", StringComparison.OrdinalIgnoreCase) ||
        Container.Contains("m4a", StringComparison.OrdinalIgnoreCase);
}

internal static class MediaProbe
{
    public static async Task<MediaInfo> ReadAsync(string path, CancellationToken token)
    {
        var result = await ProcessRunner.RunOrThrowAsync(Tools.Ffprobe,
        [
            "-v", "error",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            path,
        ], token);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;

        double duration = 0;
        long formatBitrate = 0;
        string container = string.Empty;

        if (root.TryGetProperty("format", out var format))
        {
            duration = ParseDouble(Text(format, "duration"));
            formatBitrate = ParseLong(Text(format, "bit_rate"));
            container = Text(format, "format_name") ?? string.Empty;
        }

        int width = 0, height = 0, audioChannels = 0;
        int audioTracks = 0, subtitleTracks = 0;
        double frameRate = 0;
        long videoBitrate = 0, audioBitrate = 0;
        string? videoCodec = null;
        string? audioCodec = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var kind = Text(stream, "codec_type");
                var codec = Text(stream, "codec_name");

                switch (kind)
                {
                    case "video":
                        if (codec is "mjpeg" or "png" or "bmp" or "gif") continue;
                        if (videoCodec is not null) continue;

                        videoCodec = codec;
                        width = Int(stream, "width");
                        height = Int(stream, "height");
                        frameRate = ParseRational(Text(stream, "avg_frame_rate"));
                        if (frameRate <= 0) frameRate = ParseRational(Text(stream, "r_frame_rate"));
                        videoBitrate = ParseLong(Text(stream, "bit_rate"));
                        break;

                    case "audio":
                        audioTracks++;
                        if (audioCodec is not null) continue;

                        audioCodec = codec;
                        audioBitrate = ParseLong(Text(stream, "bit_rate"));
                        audioChannels = Int(stream, "channels");
                        break;

                    case "subtitle":
                        subtitleTracks++;
                        break;
                }
            }
        }

        if (videoBitrate <= 0 && formatBitrate > 0)
        {
            videoBitrate = Math.Max(0, formatBitrate - audioBitrate);
        }

        return new MediaInfo(duration, width, height, frameRate, videoCodec, audioCodec,
                             videoBitrate, audioBitrate, audioChannels, audioTracks,
                             subtitleTracks, container);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static double ParseDouble(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static long ParseLong(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static double ParseRational(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var parts = text.Split('/');
        if (parts.Length != 2) return ParseDouble(text);

        double numerator = ParseDouble(parts[0]);
        double denominator = ParseDouble(parts[1]);
        return denominator > 0 ? numerator / denominator : 0;
    }
}
