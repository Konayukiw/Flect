using System.Diagnostics;

namespace Evals.Subtitles;

[Collection(Serial.Name)]
public sealed class SubtitlesTests
{
    [Fact]
    [Trait("Category", "AI")]
    public async Task SubtitlesTranscribeSpeech()
    {
        TestSettings.Use(settings =>
        {
            settings.Video.Subtitles.Model = SubtitleModel.Tiny;
            settings.Video.Subtitles.Language = "en";
            settings.Video.Subtitles.Output = SubtitleOutput.Srt;
        });
        var folder = Paths.NewFolder("ai-subtitles");
        var input = await Fixtures.CopyAsync("speech.wav", folder);
        var clock = Stopwatch.StartNew();

        await Rig.RunCleanAsync("audio.subtitles", input);

        var subtitles = await File.ReadAllTextAsync(Path.Combine(folder, "speech.srt"), TestContext.Current.CancellationToken);
        Assert.Contains("-->", subtitles);
        Assert.Matches("(?i)hello|world|subtitle|test", subtitles);
        TestContext.Current.SendDiagnosticMessage($"Transcription finished in {clock.Elapsed.TotalSeconds:0}s");
    }
}
