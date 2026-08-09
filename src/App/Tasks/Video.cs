using System.Globalization;
using Optimizer.Main;
using Optimizer.Main.Compression;
using Optimizer.Gui;

namespace Optimizer.Tasks;

internal abstract class VideoTask(TaskRequest request) : BatchTask(request)
{
    protected static VideoSettings Preferences => Settings.Current.Video;

    protected static string[] VideoEncoder(string extension)
    {
        var encoder = Encoders.ForContainer(extension, Preferences);
        return
        [
            .. Encoders.Quality(encoder, Preferences.Priority),
            .. Encoders.PixelFormat(encoder),
        ];
    }

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

        string[] Build(string[] audio, int threads) =>
            [.. Ffmpeg.Preamble, "-i", input, "-vf", filter,
             "-threads", threads.ToString(CultureInfo.InvariantCulture),
             .. VideoEncoder(extension), .. audio, output];

        using var working = new WorkingFile(output);
        try
        {
            await Ffmpeg.RunEncodeAsync(threads => Build(["-c:a", "copy"], threads), progress, label,
                                        info.DurationSeconds, info.Height);
        }
        catch (InvalidOperationException)
        {
            OutputPath.SafeDelete(output);
            await Ffmpeg.RunEncodeAsync(threads => Build(AudioEncoder(extension), threads), progress,
                                        label, info.DurationSeconds, info.Height);
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

internal sealed class VideoResize(TaskRequest request) : VideoTask(request)
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

internal sealed class VideoRotate(TaskRequest request) : VideoTask(request)
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

internal sealed class VideoConvert(TaskRequest request) : VideoTask(request)
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
        string[] encodeArgs = _target == "M4A"
            ? ["-vn", .. AudioEncoder(extension)]
            : [.. VideoEncoder(extension), .. AudioEncoder(extension)];

        string[] Build(int threads) =>
            [.. Ffmpeg.Preamble, "-i", path,
             "-threads", threads.ToString(CultureInfo.InvariantCulture),
             .. encodeArgs, output];

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

        await Ffmpeg.RunEncodeAsync(Build, progress, Loc.T("label.converting"), info.DurationSeconds,
                                    info.Height);
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

internal sealed class VideoCompress(TaskRequest request) : VideoTask(request)
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
