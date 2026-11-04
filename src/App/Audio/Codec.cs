using System.Globalization;

namespace Flect.Audio;

internal static class Codec
{
    public const int LossyKbps = 192;

    public static string Extension(string target) => "." + target.ToLowerInvariant();

    public static string[] Encoder(string target) => target switch
    {
        "MP3" => ["-c:a", "libmp3lame", "-b:a", $"{LossyKbps}k"],
        "M4A" or "AAC" => ["-c:a", "aac", "-b:a", $"{LossyKbps}k"],
        "OGG" => ["-c:a", "libvorbis", "-b:a", $"{LossyKbps}k"],
        "WMA" => ["-c:a", "wmav2", "-b:a", $"{LossyKbps}k"],
        "WAV" => ["-c:a", "pcm_s16le"],
        "AIF" or "AIFF" => ["-c:a", "pcm_s16be"],
        _ => ["-c:a", "aac", "-b:a", $"{LossyKbps}k"],
    };

    public static string ForExtension(string extension) =>
        extension.TrimStart('.').ToUpperInvariant() switch
        {
            "MP3" => "MP3",
            "M4A" => "M4A",
            "AAC" => "AAC",
            "WAV" => "WAV",
            "AIF" => "AIF",
            "AIFF" => "AIFF",
            "OGG" => "OGG",
            "WMA" => "WMA",
            _ => "M4A",
        };

    public static string? CopyableFrom(string target) => target switch
    {
        "MP3" => "mp3",
        "M4A" or "AAC" => "aac",
        "OGG" => "vorbis",
        _ => null,
    };
}
