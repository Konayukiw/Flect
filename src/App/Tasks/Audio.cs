using Optimizer.Main;

namespace Optimizer.Tasks;

/// <summary>
/// Everything the audio formats have in common: how a target name maps onto an
/// ffmpeg encoder, and what its file extension should be.
/// </summary>
internal static class AudioFormat
{
    public const int LossyKbps = 192;

    public static string Extension(string target) => "." + target.ToLowerInvariant();

    /// <summary>Encoder arguments for a target format.</summary>
    public static string[] Encoder(string target) => target switch
    {
        "MP3" => ["-c:a", "libmp3lame", "-b:a", $"{LossyKbps}k"],
        "M4A" or "AAC" => ["-c:a", "aac", "-b:a", $"{LossyKbps}k"],
        "OGG" => ["-c:a", "libvorbis", "-b:a", $"{LossyKbps}k"],
        "WMA" => ["-c:a", "wmav2", "-b:a", $"{LossyKbps}k"],
        // WAV and AIFF are uncompressed containers, so a bitrate would mean nothing.
        "WAV" => ["-c:a", "pcm_s16le"],
        "AIF" or "AIFF" => ["-c:a", "pcm_s16be"],
        _ => ["-c:a", "aac", "-b:a", $"{LossyKbps}k"],
    };

    /// <summary>
    /// The codec ffprobe reports when a file already holds exactly what the target
    /// container wants, which means the stream can be copied instead of re-encoded.
    /// </summary>
    public static string? CopyableFrom(string target) => target switch
    {
        "MP3" => "mp3",
        "M4A" or "AAC" => "aac",
        "OGG" => "vorbis",
        _ => null,
    };
}

internal sealed class AudioConvert(TaskRequest request) : BatchTask(request)
{
    private string _target = "MP3";

    public override string Title => "Convert";

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP3").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await MediaProbe.ReadAsync(path, progress.Token);
        var output = OutputPath.Derive(path, string.Empty, AudioFormat.Extension(_target));

        using var working = new WorkingFile(output);
        await Ffmpeg.RunAsync(
            [.. Ffmpeg.Preamble, "-i", path, "-vn", .. AudioFormat.Encoder(_target), output],
            progress, $"Converting to {_target}", info.DurationSeconds);
        working.Keep();
    }
}

internal sealed class VideoExtractAudio(TaskRequest request) : BatchTask(request)
{
    private string _target = "MP3";

    public override string Title => "Extract Audio";

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP3").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await MediaProbe.ReadAsync(path, progress.Token);
        if (!info.HasAudio) throw new InvalidOperationException("This file has no audio track.");

        var output = OutputPath.Derive(path, string.Empty, AudioFormat.Extension(_target));
        using var working = new WorkingFile(output);

        // Lifting the existing stream out untouched is both instant and lossless, so
        // it is worth trying whenever the codec already suits the target container.
        var copyable = AudioFormat.CopyableFrom(_target);
        if (copyable is not null && string.Equals(info.AudioCodec, copyable,
                                                  StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await Ffmpeg.RunAsync(
                    [.. Ffmpeg.Preamble, "-i", path, "-vn", "-c:a", "copy", output],
                    progress, $"Extracting {_target}", info.DurationSeconds);
                working.Keep();
                return;
            }
            catch (InvalidOperationException)
            {
                // Some containers refuse a straight copy. Fall through and encode.
                OutputPath.SafeDelete(output);
            }
        }

        await Ffmpeg.RunAsync(
            [.. Ffmpeg.Preamble, "-i", path, "-vn", .. AudioFormat.Encoder(_target), output],
            progress, $"Extracting {_target}", info.DurationSeconds);
        working.Keep();
    }
}
