using System.Globalization;

namespace Flect.Video;

internal sealed class Convert(Request request) : VideoBatch(request)
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
        var info = await Probe.ReadAsync(path, progress.Token);

        if (_target == "GIF")
        {
            RequireVideo(info);
            await ToGifAsync(path, info, progress);
            return;
        }

        var extension = "." + _target.ToLowerInvariant();
        var output = Flect.Files.OutputName.Derive(path, string.Empty, extension);

        string[] copyArgs = _target == "M4A" ? ["-vn", "-c:a", "copy"] : ["-c", "copy"];

        string[] EncodeArgs(EncoderChoice encoder) => _target == "M4A"
            ? ["-vn", .. AudioEncoder(extension)]
            : [.. VideoEncoder(encoder), .. AudioEncoder(extension)];

        string[] Build(EncoderChoice encoder, int threads) =>
            [.. Ffmpeg.Preamble, "-i", path,
             "-threads", threads.ToString(CultureInfo.InvariantCulture),
             .. EncodeArgs(encoder), output];

        using var working = new Flect.Files.TempFile(output);
        try
        {
            await Ffmpeg.RunAsync([.. Ffmpeg.Preamble, "-i", path, .. copyArgs, output], progress,
                                  Loc.T("label.remuxing"), info.DurationSeconds);
            working.Keep();
            return;
        }
        catch (InvalidOperationException)
        {
            Flect.Files.OutputName.SafeDelete(output);
        }

        await Ffmpeg.RunEncodeAsync(Encoder.ForContainer(extension, Preferences), Build, progress,
                                    Loc.T("label.converting"), info.DurationSeconds, info.ShortSide);
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
        var output = Flect.Files.OutputName.Derive(path, string.Empty, ".gif");
        var palette = Flect.Files.OutputName.Scratch(output, ".png");
        var chain = GifChain();

        using var working = new Flect.Files.TempFile(output);
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
            Flect.Files.OutputName.SafeDelete(palette);
        }
        working.Keep();
    }
}
