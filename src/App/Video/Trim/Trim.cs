using System.Globalization;

namespace Flect.Video.Trim;

internal sealed class Trim(Request request) : VideoBatch(request)
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
            info = Task.Run(() => Probe.ReadAsync(path, CancellationToken.None))
                       .GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            Flect.Display.Notice.Error(Loc.T("msg.noDuration"));
            return false;
        }

        if (!info.HasVideo)
        {
            Flect.Display.Notice.Error(Loc.T("msg.noVideoStream"));
            return false;
        }
        if (info.DurationSeconds <= 0)
        {
            Flect.Display.Notice.Error(Loc.T("msg.noDuration"));
            return false;
        }

        _range = TrimWindow.Ask(path, info.DurationSeconds);
        return _range is not null;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        if (_range is null) return;

        var info = await Probe.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var start = Math.Clamp(_range.StartSeconds, 0, Math.Max(0, info.DurationSeconds - 0.05));
        var end = Math.Clamp(_range.EndSeconds, start, info.DurationSeconds);
        var length = end - start;
        if (length <= 0) throw new InvalidOperationException(Loc.T("dialog.trim.backwards"));

        var output = Flect.Files.OutputName.Derive(path, "_trimmed");
        var extension = Path.GetExtension(output).ToLowerInvariant();

        string[] Seek() =>
        [
            .. Ffmpeg.Preamble,
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
        ];

        using var working = new Flect.Files.TempFile(output);

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

            await Ffmpeg.RunEncodeAsync(Encoder.ForContainer(extension, Preferences), Build,
                                        progress, Loc.T("label.trimming"), length, info.ShortSide);
        }

        working.Keep();
    }
}
