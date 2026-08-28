namespace Optimizer.Tasks.Subtitles;

internal sealed class MediaSubtitles(Request request) : TaskBase(request)
{
    private SubtitlesSettings _settings = new();

    public override string Title => Loc.T(Request.Domain == "audio"
        ? "menu.audio.subtitles"
        : "menu.video.subtitles");

    public override bool Configure()
    {
        _settings = Settings.Current.Video.Subtitles;
        return true;
    }

    public override async Task RunAsync(ITaskProgress progress)
    {
        var extension = Extension(_settings.Output);
        var jobs = new List<(string Input, string Output, string Wav)>();
        var wavs = new List<string>();

        try
        {
            for (int index = 0; index < Request.Paths.Count; index++)
            {
                progress.Token.ThrowIfCancellationRequested();
                var path = Request.Paths[index];
                progress.Step(index, Request.Paths.Count);
                progress.Status(Path.GetFileName(path));

                MediaInfo info;
                try
                {
                    info = await Media.ReadAsync(path, progress.Token);
                }
                catch (Exception ex)
                {
                    progress.Error($"{Path.GetFileName(path)} — {ex.Message}");
                    continue;
                }

                if (!info.HasAudio)
                {
                    progress.Error($"{Path.GetFileName(path)} — {Loc.T("msg.noAudioTrack")}");
                    continue;
                }
                if (info.DurationSeconds <= 0)
                {
                    progress.Error($"{Path.GetFileName(path)} — {Loc.T("msg.noDuration")}");
                    continue;
                }

                var output = OutputPath.Derive(path, string.Empty, extension);
                var wav = OutputPath.Scratch(output, ".wav");
                try
                {
                    await Ffmpeg.RunAsync(
                        [.. Ffmpeg.Preamble, "-i", path, "-vn",
                         "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", wav],
                        progress, Loc.T("label.extractingAudio"), info.DurationSeconds);
                }
                catch (Exception ex)
                {
                    OutputPath.SafeDelete(wav);
                    progress.Error($"{Path.GetFileName(path)} — {ex.Message}");
                    continue;
                }

                jobs.Add((path, output, wav));
                wavs.Add(wav);
            }

            if (jobs.Count == 0) return;

            var results = await SubtitleSupport.TranscribeBatchAsync(jobs, _settings,
                                                                    progress, progress.Token);

            for (int index = 0; index < results.Count; index++)
            {
                progress.Token.ThrowIfCancellationRequested();
                var result = results[index];
                progress.Step(index + 1, jobs.Count);

                if (result.Empty)
                {
                    OutputPath.SafeDelete(result.Output);
                    progress.Warn(Loc.F("msg.noTextFound", Path.GetFileName(result.Input)));
                    continue;
                }
                if (result.Ok && File.Exists(result.Output)) continue;

                OutputPath.SafeDelete(result.Output);
                progress.Error($"{Path.GetFileName(result.Input)} — " +
                               (result.Error ?? Loc.T("msg.subtitlesFailed")));
            }
        }
        finally
        {
            foreach (var wav in wavs) OutputPath.SafeDelete(wav);
        }
    }

    private static string Extension(SubtitleOutput output) => output switch
    {
        SubtitleOutput.Vtt => ".vtt",
        SubtitleOutput.Txt => ".txt",
        _ => ".srt",
    };
}
