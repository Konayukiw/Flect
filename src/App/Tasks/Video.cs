using System.Globalization;
using Optimizer.Main;
using Optimizer.Main.Compression;
using Optimizer.Gui;

namespace Optimizer.Tasks;

internal abstract class VideoTask(TaskRequest request) : BatchTask(request)
{
    protected static string[] VideoCodec(string extension) => extension switch
    {
        ".webm" => ["-c:v", "libvpx-vp9"],
        ".avi" => ["-c:v", "mpeg4"],
        _ => ["-c:v", "libx264", "-preset", "medium", "-pix_fmt", "yuv420p"],
    };

    protected static string[] VideoEncoder(string extension) => extension switch
    {
        ".webm" => [.. VideoCodec(extension), "-crf", "32", "-b:v", "0"],
        ".avi" => [.. VideoCodec(extension), "-qscale:v", "3"],
        _ => [.. VideoCodec(extension), "-crf", "20"],
    };

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
        if (!info.HasVideo) throw new InvalidOperationException("This file has no video stream.");
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

    public override string Title => "Resize";

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
        await FilterAsync(path, output, ScaleFilter(), info, progress, "Resizing");
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

    public override string Title => "Rotate";

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
        await FilterAsync(path, output, RotateFilter(), info, progress, "Rotating");
    }

    private string RotateFilter() => _degrees switch
    {
        90 => $"transpose=1,{Ffmpeg.EvenDimensions}",
        180 => $"transpose=1,transpose=1,{Ffmpeg.EvenDimensions}",
        _ => $"rotate={_degrees}*PI/180:ow=rotw({_degrees}*PI/180):oh=roth({_degrees}*PI/180):" +
             $"fillcolor=black,{Ffmpeg.EvenDimensions}",
    };
}

internal sealed class VideoConvert(TaskRequest request) : VideoTask(request)
{
    private const string GifChain = "fps=15,scale='min(640,iw)':-2:flags=lanczos";

    private string _target = "MP4";

    public override string Title => "Convert";

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
                                  "Remuxing", info.DurationSeconds);
            working.Keep();
            return;
        }
        catch (InvalidOperationException)
        {
            OutputPath.SafeDelete(output);
        }

        await Ffmpeg.RunEncodeAsync(Build, progress, "Converting", info.DurationSeconds, info.Height);
        working.Keep();
    }

    private static async Task ToGifAsync(string path, MediaInfo info, ITaskProgress progress)
    {
        var output = OutputPath.Derive(path, string.Empty, ".gif");
        var palette = OutputPath.Scratch(output, ".png");

        using var working = new WorkingFile(output);
        try
        {
            await Ffmpeg.RunAsync(
                [.. Ffmpeg.Preamble, "-i", path, "-vf", $"{GifChain},palettegen=stats_mode=diff",
                 palette],
                progress, "Building palette", info.DurationSeconds);

            await Ffmpeg.RunAsync(
                [.. Ffmpeg.Preamble, "-i", path, "-i", palette, "-lavfi",
                 $"{GifChain}[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle",
                 "-loop", "0", output],
                progress, "Writing GIF", info.DurationSeconds);
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
    private const long Mebibyte = 1024 * 1024;

    private long _target;

    public override string Title => "Compress";

    public override bool Configure()
    {
        if (string.Equals(Request["preset"], "discord", StringComparison.OrdinalIgnoreCase))
        {
            _target = 10 * Mebibyte;
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
            progress.Info(
                $"{Path.GetFileName(path)} is already {Formatting.Bytes(original)} — left alone.");
            return;
        }

        var info = await Media.ReadAsync(path, progress.Token);
        if (info.DurationSeconds <= 0)
        {
            throw new InvalidOperationException("Could not read the duration of this file.");
        }

        var plan = PlanBuilder.Build(info, _target);
        var destination = OutputPath.Derive(path, "_compressed", info.HasVideo ? ".mp4" : ".m4a");

        using var scratch = new ScratchDirectory(destination);
        var pipeline = new CompressionPipeline(plan, progress);

        var outcome = info.HasVideo
            ? await pipeline.RunAsync(path, scratch)
            : await pipeline.RunAudioOnlySourceAsync(path, scratch);

        File.Move(outcome.Path, destination);

        if (outcome.Bytes > _target)
        {
            progress.Warn($"{Path.GetFileName(path)} — Landed at " +
                          $"{Formatting.Bytes(outcome.Bytes)}, over the " +
                          $"{Formatting.Bytes(_target)} target.");
        }
    }
}
