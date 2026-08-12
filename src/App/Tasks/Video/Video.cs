using System.Globalization;
using Optimizer.Main.Process;

namespace Optimizer.Tasks.Video;

internal abstract class VideoTask(Request request) : BatchTask(request)
{
    protected static VideoSettings Preferences => Settings.Current.Video;

    protected static string[] VideoEncoder(EncoderChoice encoder) =>
    [
        .. Encoders.Quality(encoder, Preferences.Priority),
        .. Encoders.PixelFormat(encoder),
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

        using var working = new WorkingFile(output);
        try
        {
            await Ffmpeg.RunEncodeAsync(Encoders.ForContainer(extension, Preferences),
                                        (encoder, threads) => Build(["-c:a", "copy"], encoder, threads),
                                        progress, label, info.DurationSeconds, info.Height);
        }
        catch (InvalidOperationException)
        {
            OutputPath.SafeDelete(output);
            await Ffmpeg.RunEncodeAsync(Encoders.ForContainer(extension, Preferences),
                                        (encoder, threads) =>
                                            Build(AudioEncoder(extension), encoder, threads),
                                        progress, label, info.DurationSeconds, info.Height);
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

internal sealed class VideoResize(Request request) : VideoTask(request)
{
    private PixelTarget? _pixels;
    private double _percent = 50;

    public override string Title => Loc.T("menu.video.resize");

    public override bool Configure()
    {
        var scale = Request["scale"];
        if (!string.Equals(scale, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(scale, out var value) && value > 0) _percent = value;
            return true;
        }
        _pixels = Pixels.Ask();
        return _pixels is not null;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Media.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var output = OutputPath.Derive(path, "_resized");
        await FilterAsync(path, output, ScaleFilter(), info, progress, Loc.T("label.resizing"));
    }

    private string ScaleFilter()
    {
        if (_pixels is null)
        {
            var factor = (_percent / 100).ToString(CultureInfo.InvariantCulture);
            return $"scale=trunc(iw*{factor}/2)*2:trunc(ih*{factor}/2)*2";
        }

        uint width = _pixels.Width;
        uint height = _pixels.Height;

        if (width > 0 && height > 0)
        {
            return _pixels.KeepAspect
                ? $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                  Ffmpeg.EvenDimensions
                : $"scale={width - width % 2}:{height - height % 2}";
        }
        return width > 0 ? $"scale={width - width % 2}:-2" : $"scale=-2:{height - height % 2}";
    }
}

internal sealed class VideoRotate(Request request) : VideoTask(request)
{
    private int _degrees = 90;

    public override string Title => Loc.T("menu.video.rotate");

    public override bool Configure()
    {
        if (int.TryParse(Request["deg"], out var value)) _degrees = value;
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Media.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var output = OutputPath.Derive(path, "_rotated");
        await FilterAsync(path, output, RotateFilter(), info, progress, Loc.T("label.rotating"));
    }

    private string RotateFilter() => _degrees switch
    {
        90 => $"transpose=1,{Ffmpeg.EvenDimensions}",
        180 => $"transpose=1,transpose=1,{Ffmpeg.EvenDimensions}",
        _ => $"rotate={_degrees}*PI/180:ow=rotw({_degrees}*PI/180):oh=roth({_degrees}*PI/180):" +
             $"fillcolor={ColorText.ForFfmpeg(Preferences.RotateFillColor)}," +
             Ffmpeg.EvenDimensions,
    };
}

internal sealed class VideoConvert(Request request) : VideoTask(request)
{
    private string _target = "MP4";

    public override string Title => Loc.T("menu.video.convert");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP4").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Media.ReadAsync(path, progress.Token);

        if (_target == "GIF")
        {
            RequireVideo(info);
            await ToGifAsync(path, info, progress);
            return;
        }

        var extension = "." + _target.ToLowerInvariant();
        var output = OutputPath.Derive(path, string.Empty, extension);

        string[] copyArgs = _target == "M4A" ? ["-vn", "-c:a", "copy"] : ["-c", "copy"];

        string[] EncodeArgs(EncoderChoice encoder) => _target == "M4A"
            ? ["-vn", .. AudioEncoder(extension)]
            : [.. VideoEncoder(encoder), .. AudioEncoder(extension)];

        string[] Build(EncoderChoice encoder, int threads) =>
            [.. Ffmpeg.Preamble, "-i", path,
             "-threads", threads.ToString(CultureInfo.InvariantCulture),
             .. EncodeArgs(encoder), output];

        using var working = new WorkingFile(output);
        try
        {
            await Ffmpeg.RunAsync([.. Ffmpeg.Preamble, "-i", path, .. copyArgs, output], progress,
                                  Loc.T("label.remuxing"), info.DurationSeconds);
            working.Keep();
            return;
        }
        catch (InvalidOperationException)
        {
            OutputPath.SafeDelete(output);
        }

        await Ffmpeg.RunEncodeAsync(Encoders.ForContainer(extension, Preferences), Build, progress,
                                    Loc.T("label.converting"), info.DurationSeconds, info.Height);
        working.Keep();
    }

    private static string GifChain()
    {
        var video = Preferences;
        return $"fps={video.GifFps}," +
               $"scale='min({video.GifMaxWidth},iw)':-2:flags=lanczos";
    }

    private static string PaletteUse() => Preferences.GifDither switch
    {
        GifDither.None => "paletteuse=dither=none:diff_mode=rectangle",
        GifDither.FloydSteinberg => "paletteuse=dither=floyd_steinberg:diff_mode=rectangle",
        _ => "paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle",
    };

    private static async Task ToGifAsync(string path, MediaInfo info, ITaskProgress progress)
    {
        var output = OutputPath.Derive(path, string.Empty, ".gif");
        var palette = OutputPath.Scratch(output, ".png");
        var chain = GifChain();

        using var working = new WorkingFile(output);
        try
        {
            await Ffmpeg.RunAsync(
                [.. Ffmpeg.Preamble, "-i", path, "-vf", $"{chain},palettegen=stats_mode=diff",
                 palette],
                progress, Loc.T("label.buildingPalette"), info.DurationSeconds);

            await Ffmpeg.RunAsync(
                [.. Ffmpeg.Preamble, "-i", path, "-i", palette, "-lavfi",
                 $"{chain}[x];[x][1:v]{PaletteUse()}", "-loop", "0", output],
                progress, Loc.T("label.writingGif"), info.DurationSeconds);
        }
        finally
        {
            OutputPath.SafeDelete(palette);
        }
        working.Keep();
    }
}

internal sealed class VideoTrim(Request request) : VideoTask(request)
{
    private TrimRange? _range;

    public override string Title => Loc.T("menu.video.trim");

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

        if (!info.HasVideo)
        {
            Report.Error(Loc.T("msg.noVideoStream"));
            return false;
        }
        if (info.DurationSeconds <= 0)
        {
            Report.Error(Loc.T("msg.noDuration"));
            return false;
        }

        _range = Trim.Ask(path, info.DurationSeconds);
        return _range is not null;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        if (_range is null) return;

        var info = await Media.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var start = Math.Clamp(_range.StartSeconds, 0, Math.Max(0, info.DurationSeconds - 0.05));
        var end = Math.Clamp(_range.EndSeconds, start, info.DurationSeconds);
        var length = end - start;
        if (length <= 0) throw new InvalidOperationException(Loc.T("dialog.trim.backwards"));

        var output = OutputPath.Derive(path, "_trimmed");
        var extension = Path.GetExtension(output).ToLowerInvariant();

        string[] Seek() =>
        [
            .. Ffmpeg.Preamble,
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
        ];

        using var working = new WorkingFile(output);

        if (Preferences.Trim.Accuracy == TrimAccuracy.Keyframe)
        {
            await Ffmpeg.RunAsync([.. Seek(), "-c", "copy", "-avoid_negative_ts", "make_zero",
                                   output],
                                  progress, Loc.T("label.trimming"), length);
        }
        else
        {
            string[] Build(EncoderChoice encoder, int threads) =>
                [.. Seek(),
                 "-threads", threads.ToString(CultureInfo.InvariantCulture),
                 .. VideoEncoder(encoder), .. AudioEncoder(extension), output];

            await Ffmpeg.RunEncodeAsync(Encoders.ForContainer(extension, Preferences), Build,
                                        progress, Loc.T("label.trimming"), length, info.Height);
        }

        working.Keep();
    }
}

internal sealed class VideoCompress(Request request) : VideoTask(request)
{
    private long _target;

    public override string Title => Loc.T("menu.video.compress");

    public override bool Configure()
    {
        if (string.Equals(Request["preset"], "discord", StringComparison.OrdinalIgnoreCase))
        {
            _target = Preferences.Presets.CompressDiscord;
            return true;
        }

        var bytes = Request["bytes"];
        if (string.Equals(bytes, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var answer = TargetBytes.Ask(LargestSelected());
            if (answer is null) return false;
            _target = answer.Value;
            return true;
        }
        return long.TryParse(bytes, out _target) && _target > 0;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        long original = new FileInfo(path).Length;
        if (original <= Bitrate.SafeTarget(_target))
        {
            progress.Info(Loc.F("msg.alreadySmall", Path.GetFileName(path),
                                Formatting.Bytes(original)));
            return;
        }

        var info = await Media.ReadAsync(path, progress.Token);
        if (info.DurationSeconds <= 0)
        {
            throw new InvalidOperationException(Loc.T("msg.noDuration"));
        }

        var plan = Planner.Build(info, _target);
        var destination = OutputPath.Derive(path, "_compressed", info.HasVideo ? ".mp4" : ".m4a");

        using var scratch = new ScratchDirectory(destination);
        var pipeline = new Pipeline(plan, progress);

        var outcome = info.HasVideo
            ? await pipeline.RunAsync(path, scratch)
            : await pipeline.RunAudioOnlySourceAsync(path, scratch);

        File.Move(outcome.Path, destination);

        if (outcome.Bytes > _target)
        {
            progress.Warn(Loc.F("msg.overTarget", Path.GetFileName(path),
                                Formatting.Bytes(outcome.Bytes), Formatting.Bytes(_target)));
        }
    }
}
