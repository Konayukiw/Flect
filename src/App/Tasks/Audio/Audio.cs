using System.Globalization;
using Optimizer.Main.Process;

namespace Optimizer.Tasks.Audio;

internal static class AudioFormat
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

internal sealed class AudioConvert(Request request) : BatchTask(request)
{
    private string _target = "MP3";

    public override string Title => Loc.T("menu.audio.convert");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP3").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Media.ReadAsync(path, progress.Token);
        var output = OutputPath.Derive(path, string.Empty, AudioFormat.Extension(_target));

        using var working = new WorkingFile(output);
        await Ffmpeg.RunAsync(
            [.. Ffmpeg.Preamble, "-i", path, "-vn", .. AudioFormat.Encoder(_target), output],
            progress, Loc.F("label.convertingTo", _target), info.DurationSeconds);
        working.Keep();
    }
}

internal sealed class VideoExtractAudio(Request request) : BatchTask(request)
{
    private string _target = "MP3";

    public override string Title => Loc.T("menu.video.extractAudio");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP3").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Media.ReadAsync(path, progress.Token);
        if (!info.HasAudio) throw new InvalidOperationException(Loc.T("msg.noAudioTrack"));

        var output = OutputPath.Derive(path, string.Empty, AudioFormat.Extension(_target));
        using var working = new WorkingFile(output);

        var copyable = AudioFormat.CopyableFrom(_target);
        if (copyable is not null && string.Equals(info.AudioCodec, copyable,
                                                  StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await Ffmpeg.RunAsync(
                    [.. Ffmpeg.Preamble, "-i", path, "-vn", "-c:a", "copy", output],
                    progress, Loc.F("label.extracting", _target), info.DurationSeconds);
                working.Keep();
                return;
            }
            catch (InvalidOperationException)
            {
                OutputPath.SafeDelete(output);
            }
        }

        await Ffmpeg.RunAsync(
            [.. Ffmpeg.Preamble, "-i", path, "-vn", .. AudioFormat.Encoder(_target), output],
            progress, Loc.F("label.extracting", _target), info.DurationSeconds);
        working.Keep();
    }
}

internal sealed class AudioTrim(Request request) : BatchTask(request)
{
    private TrimRange? _range;

    public override string Title => Loc.T("menu.audio.trim");

    public override bool Configure()
    {
        var path = Request.Paths.FirstOrDefault();
        if (path is null) return false;

        MediaInfo info;
        try
        {
            info = Task.Run(() => Media.ReadAsync(path, CancellationToken.None))
                       .GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            Report.Error(Loc.T("msg.noDuration"));
            return false;
        }

        if (!info.HasAudio)
        {
            Report.Error(Loc.T("msg.noAudioTrack"));
            return false;
        }
        if (info.DurationSeconds <= 0)
        {
            Report.Error(Loc.T("msg.noDuration"));
            return false;
        }

        _range = Trim.Ask(path, info.DurationSeconds, picture: false);
        return _range is not null;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        if (_range is null) return;

        var info = await Media.ReadAsync(path, progress.Token);
        if (!info.HasAudio) throw new InvalidOperationException(Loc.T("msg.noAudioTrack"));

        var start = Math.Clamp(_range.StartSeconds, 0, Math.Max(0, info.DurationSeconds - 0.05));
        var end = Math.Clamp(_range.EndSeconds, start, info.DurationSeconds);
        var length = end - start;
        if (length <= 0) throw new InvalidOperationException(Loc.T("dialog.trim.backwards"));

        var output = OutputPath.Derive(path, "_trimmed");
        var extension = Path.GetExtension(output);

        string[] Seek() =>
        [
            .. Ffmpeg.Preamble,
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
        ];

        using var working = new WorkingFile(output);

        if (Settings.Current.Video.Trim.Accuracy == TrimAccuracy.Keyframe)
        {
            await Ffmpeg.RunAsync([.. Seek(), "-c", "copy", "-avoid_negative_ts", "make_zero",
                                   output],
                                  progress, Loc.T("label.trimming"), length);
        }
        else
        {
            await Ffmpeg.RunAsync(
                [.. Seek(), "-vn", .. AudioFormat.Encoder(AudioFormat.ForExtension(extension)), output],
                progress, Loc.T("label.trimming"), length);
        }

        working.Keep();
    }
}
