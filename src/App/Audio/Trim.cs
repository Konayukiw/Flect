using System.Globalization;

namespace Flect.Audio;

internal sealed class Trim(Request request) : Flect.Dispatch.Batch(request)
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
            info = Task.Run(() => Flect.Video.Probe.ReadAsync(path, CancellationToken.None))
                       .GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            Flect.Display.Notice.Error(Loc.T("msg.noDuration"));
            return false;
        }

        if (!info.HasAudio)
        {
            Flect.Display.Notice.Error(Loc.T("msg.noAudioTrack"));
            return false;
        }
        if (info.DurationSeconds <= 0)
        {
            Flect.Display.Notice.Error(Loc.T("msg.noDuration"));
            return false;
        }

        _range = TrimWindow.Ask(path, info.DurationSeconds, picture: false);
        return _range is not null;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        if (_range is null) return;

        var info = await Flect.Video.Probe.ReadAsync(path, progress.Token);
        if (!info.HasAudio) throw new InvalidOperationException(Loc.T("msg.noAudioTrack"));

        var start = Math.Clamp(_range.StartSeconds, 0, Math.Max(0, info.DurationSeconds - 0.05));
        var end = Math.Clamp(_range.EndSeconds, start, info.DurationSeconds);
        var length = end - start;
        if (length <= 0) throw new InvalidOperationException(Loc.T("dialog.trim.backwards"));

        var output = Flect.Files.OutputName.Derive(path, "_trimmed");
        var extension = Path.GetExtension(output);

        string[] Seek() =>
        [
            .. Ffmpeg.Preamble,
            "-ss", start.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-t", length.ToString("0.###", CultureInfo.InvariantCulture),
        ];

        using var working = new Flect.Files.TempFile(output);

        if (Preferences.Current.Video.Trim.Accuracy == TrimAccuracy.Keyframe)
        {
            await Ffmpeg.RunAsync([.. Seek(), "-c", "copy", "-avoid_negative_ts", "make_zero",
                                   output],
                                  progress, Loc.T("label.trimming"), length);
        }
        else
        {
            await Ffmpeg.RunAsync(
                [.. Seek(), "-vn", .. Codec.Encoder(Codec.ForExtension(extension)), output],
                progress, Loc.T("label.trimming"), length);
        }

        working.Keep();
    }
}
