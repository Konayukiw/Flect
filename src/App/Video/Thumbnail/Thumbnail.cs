using System.Globalization;

namespace Flect.Video.Thumbnail;

internal sealed class Thumbnail(Request request) : VideoBatch(request)
{
    private double _time;

    public override string Title => Loc.T("menu.video.thumbnail");

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

        var time = ThumbnailWindow.Ask(path, info.DurationSeconds);
        if (time is null) return false;

        _time = Math.Clamp(time.Value, 0, info.DurationSeconds);
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Probe.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var settings = Preferences.Thumbnail;
        var format = settings.Format == ThumbnailFormat.Jpg ? "jpg" : "png";
        var output = Flect.Files.OutputName.Derive(path, "_thumbnail", "." + format);

        string[] Build()
        {
            var args = new List<string>(Ffmpeg.Preamble);
            args.Add("-ss");
            args.Add(_time.ToString("0.###", CultureInfo.InvariantCulture));
            args.Add("-i");
            args.Add(path);
            args.Add("-frames:v");
            args.Add("1");

            if (settings.MaxWidth > 0)
            {
                args.Add("-vf");
                args.Add($"scale='min({settings.MaxWidth},iw)':-2");
            }
            if (settings.Format == ThumbnailFormat.Jpg)
            {
                args.Add("-q:v");
                args.Add((31 - Math.Clamp(settings.Quality, 1, 100) * 29 / 100)
                    .ToString(CultureInfo.InvariantCulture));
            }

            args.Add(output);
            return [.. args];
        }

        using var working = new Flect.Files.TempFile(output);
        await Ffmpeg.RunAsync(Build(), progress, Loc.T("label.exporting"), info.DurationSeconds);
        working.Keep();
    }
}
