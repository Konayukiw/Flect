
namespace Evals.Audio;

[Collection(Serial.Name)]
public sealed class AudioTests
{
    [Theory]
    [InlineData("M4A", "aac")]
    [InlineData("WAV", "pcm_s16le")]
    [InlineData("AIF", "pcm_s16be")]
    [InlineData("AAC", "aac")]
    [InlineData("OGG", "vorbis")]
    [InlineData("WMA", "wmav2")]
    public async Task ConvertFromMp3(string target, string codec)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("audio-convert");
        var input = await Fixtures.CopyAsync("tone.mp3", folder);

        await Rig.RunCleanAsync($"audio.convert?to={target}", input);

        Assert.Equal(codec, await Tool.AudioCodecAsync(Path.Combine(folder, "tone." + target.ToLowerInvariant())));
    }

    [Fact]
    public async Task ConvertFromWavToMp3()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("audio-mp3");
        var input = await Fixtures.CopyAsync("tone.wav", folder);

        await Rig.RunCleanAsync("audio.convert?to=MP3", input);

        Assert.Equal("mp3", await Tool.AudioCodecAsync(Path.Combine(folder, "tone.mp3")));
    }

    [Theory]
    [InlineData("Keyframe")]
    [InlineData("Exact")]
    public async Task TrimKeepsTheChosenRange(string accuracy)
    {
        TestSettings.Use(settings => settings.Video.Trim.Accuracy = Enum.Parse<TrimAccuracy>(accuracy));
        var folder = Paths.NewFolder("audio-trim");
        var input = await Fixtures.CopyAsync("tone.mp3", folder);
        var task = Rig.Create("audio.trim", input);
        Rig.Set(task, "_range", new TrimRange(0.5, 2));

        var progress = await Rig.RunAsync(task, configure: false,
                                              token: TestContext.Current.CancellationToken);

        progress.AssertClean();
        var output = Path.Combine(folder, "tone_trimmed.mp3");
        Assert.Equal("mp3", await Tool.AudioCodecAsync(output));
        Assert.InRange(await Tool.DurationAsync(output), 1.2, 1.9);
        Assert.True(File.Exists(input));
    }
}
