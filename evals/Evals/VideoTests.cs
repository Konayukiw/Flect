using Evals.Infrastructure;
using Optimizer.Gui.Dialogs;

namespace Evals;

[Collection(Serial.Name)]
public sealed class VideoTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResizeHalvesTheFrame()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-resize");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync("video.resize?scale=50", input);

        Assert.Equal((320, 180), await Tool.SizeAsync(Path.Combine(folder, "clip_resized.mp4")));
    }

    [Fact]
    public async Task RotateNinetyTurnsTheFrame()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-rotate");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync("video.rotate?deg=90", input);

        Assert.Equal((360, 640), await Tool.SizeAsync(Path.Combine(folder, "clip_rotated.mp4")));
    }

    [Fact]
    public async Task RotateByAFractionalAngleWidensTheFrame()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-rotate-angle");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync("video.rotate?deg=22.5", input);

        var (width, height) = await Tool.SizeAsync(Path.Combine(folder, "clip_rotated.mp4"));
        Assert.True(width > 640 && height > 360, $"{width}x{height}");
    }

    [Theory]
    [InlineData("MKV", "h264")]
    [InlineData("MOV", "h264")]
    [InlineData("FLV", "h264")]
    [InlineData("WEBM", "vp9")]
    public async Task ConvertChangesTheContainer(string target, string codec)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-convert");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync($"video.convert?to={target}", input);

        var output = Path.Combine(folder, "clip." + target.ToLowerInvariant());
        Assert.Equal(codec, await Tool.VideoCodecAsync(output));
    }

    [Fact]
    public async Task ConvertToAviWritesAPlayableFile()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-avi");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync("video.convert?to=AVI", input);

        Assert.InRange(await Tool.DurationAsync(Path.Combine(folder, "clip.avi")), 3.5, 4.5);
    }

    [Fact]
    public async Task ConvertToMovFallsBackToH264ForVp9Settings()
    {
        TestSettings.Use(settings => settings.Video.Codec = VideoCodecChoice.Vp9);
        var folder = Paths.NewFolder("video-mov-vp9");
        var input = await Fixtures.CopyAsync("clip.mp4", folder, "clip.mkv");
        await Tool.FfmpegAsync("-i", input, "-c:v", "libvpx-vp9", "-deadline", "realtime", "-c:a", "libopus",
                               Path.Combine(folder, "source.webm"));

        await Harness.RunCleanAsync("video.resize?scale=50", Path.Combine(folder, "source.webm"));
        await Harness.RunCleanAsync("video.convert?to=MOV", Path.Combine(folder, "source.webm"));

        Assert.Equal("vp9", await Tool.VideoCodecAsync(Path.Combine(folder, "source_resized.webm")));
        Assert.Equal("h264", await Tool.VideoCodecAsync(Path.Combine(folder, "source.mov")));
    }

    [Fact]
    public async Task ConvertToM4AKeepsOnlyTheSound()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-m4a");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync("video.convert?to=M4A", input);

        var output = Path.Combine(folder, "clip.m4a");
        Assert.Equal("aac", await Tool.AudioCodecAsync(output));
        Assert.Empty(await Tool.ProbeAsync(output, "v", "stream=codec_name"));
    }

    [Fact]
    public async Task ConvertToGifAnimates()
    {
        TestSettings.Use(settings => settings.Video.GifMaxWidth = 320);
        var folder = Paths.NewFolder("video-gif");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync("video.convert?to=GIF", input);

        var output = Path.Combine(folder, "clip.gif");
        Assert.Equal("gif", await Tool.VideoCodecAsync(output));
        Assert.Equal(320, (await Tool.SizeAsync(output)).Width);
    }

    [Theory]
    [InlineData("Keyframe")]
    [InlineData("Exact")]
    public async Task TrimKeepsTheChosenRange(string accuracy)
    {
        TestSettings.Use(settings => settings.Video.Trim.Accuracy = Enum.Parse<TrimAccuracy>(accuracy));
        var folder = Paths.NewFolder("video-trim");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);
        var task = Harness.Create("video.trim", input);
        Harness.Set(task, "_range", new TrimRange(1, 3));

        var progress = await Harness.RunAsync(task, configure: false, token: Token);

        progress.AssertClean();
        Assert.InRange(await Tool.DurationAsync(Path.Combine(folder, "clip_trimmed.mp4")), 1.8, 2.3);
    }

    [Theory]
    [InlineData("Png", 0, "clip_thumbnail.png", 640)]
    [InlineData("Jpg", 320, "clip_thumbnail.jpg", 320)]
    public async Task ThumbnailExportsOneFrame(string format, int maxWidth, string name, int width)
    {
        TestSettings.Use(settings =>
        {
            settings.Video.Thumbnail.Format = Enum.Parse<ThumbnailFormat>(format);
            settings.Video.Thumbnail.MaxWidth = maxWidth;
        });
        var folder = Paths.NewFolder("video-thumbnail");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);
        var task = Harness.Create("video.thumbnail", input);
        Harness.Set(task, "_time", 1.5);

        var progress = await Harness.RunAsync(task, configure: false, token: Token);

        progress.AssertClean();
        using var image = new ImageMagick.MagickImage(Path.Combine(folder, name));
        Assert.Equal((uint)width, image.Width);
    }

    [Fact]
    public async Task CompressKeepsAPhoneVideoUprightWithSquarePixels()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-compress-portrait");
        var input = await Fixtures.CopyAsync("portrait.mp4", folder);

        var progress = await Harness.RunCleanAsync("video.compress?bytes=153600", input);

        var output = Path.Combine(folder, "portrait_compressed.mp4");
        var stream = await Tool.ProbeAsync(output, "v:0", "stream=width,height,sample_aspect_ratio");
        Assert.True(int.Parse(stream["width"]) < int.Parse(stream["height"]), $"{stream["width"]}x{stream["height"]}");
        Assert.Equal("1:1", stream["sample_aspect_ratio"]);
        Assert.True(new FileInfo(output).Length <= 153600 || progress.Warnings.Count > 0);
    }

    [Fact]
    public async Task CompressWithTheDiscordPresetUsesTheSavedLimit()
    {
        TestSettings.Use(settings => settings.Video.Presets.CompressDiscord = 200 * 1024);
        var folder = Paths.NewFolder("video-compress-discord");
        var input = await Fixtures.CopyAsync("wide.mp4", folder);

        var progress = await Harness.RunCleanAsync("video.compress?preset=discord", input);

        var output = Path.Combine(folder, "wide_compressed.mp4");
        Assert.True(new FileInfo(output).Length <= 200 * 1024 || progress.Warnings.Count > 0);
        Assert.Empty(Directory.GetDirectories(folder, ".Flect-*"));
    }

    [Fact]
    public async Task CompressIntoWebMSwapsTheCodecs()
    {
        TestSettings.Use(settings =>
        {
            settings.Video.Container = VideoContainer.WebM;
            settings.Video.Codec = VideoCodecChoice.H264;
        });
        var folder = Paths.NewFolder("video-compress-webm");
        var input = await Fixtures.CopyAsync("wide.mp4", folder);

        await Harness.RunCleanAsync("video.compress?bytes=204800", input);

        var output = Path.Combine(folder, "wide_compressed.webm");
        Assert.Equal("vp9", await Tool.VideoCodecAsync(output));
        Assert.Equal("opus", await Tool.AudioCodecAsync(output));
    }

    [Fact]
    public async Task CompressLeavesSmallFilesAlone()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-compress-small");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        var progress = await Harness.RunCleanAsync("video.compress?bytes=104857600", input);

        Assert.False(File.Exists(Path.Combine(folder, "clip_compressed.mp4")));
        Assert.NotEmpty(progress.Infos);
    }

    [Theory]
    [InlineData("MP3", "mp3")]
    [InlineData("M4A", "aac")]
    [InlineData("WAV", "pcm_s16le")]
    [InlineData("AIFF", "pcm_s16be")]
    [InlineData("AAC", "aac")]
    [InlineData("OGG", "vorbis")]
    [InlineData("WMA", "wmav2")]
    public async Task ExtractAudioWritesTheSoundtrack(string target, string codec)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("video-extract");
        var input = await Fixtures.CopyAsync("clip.mp4", folder);

        await Harness.RunCleanAsync($"video.extractAudio?to={target}", input);

        Assert.Equal(codec, await Tool.AudioCodecAsync(Path.Combine(folder, "clip." + target.ToLowerInvariant())));
    }

    [Fact]
    public async Task CancellingAnEncodeLeavesNothingBehind()
    {
        TestSettings.Use(settings => settings.Video.Priority = EncodePriority.Quality);
        var folder = Paths.NewFolder("video-cancel");
        var input = await Fixtures.CopyAsync("wide.mp4", folder);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Harness.RunAsync(Harness.Create("video.rotate?deg=33", input), token: cancel.Token));

        Assert.False(File.Exists(Path.Combine(folder, "wide_rotated.mp4")));
    }
}

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

        await Harness.RunCleanAsync($"audio.convert?to={target}", input);

        Assert.Equal(codec, await Tool.AudioCodecAsync(Path.Combine(folder, "tone." + target.ToLowerInvariant())));
    }

    [Fact]
    public async Task ConvertFromWavToMp3()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("audio-mp3");
        var input = await Fixtures.CopyAsync("tone.wav", folder);

        await Harness.RunCleanAsync("audio.convert?to=MP3", input);

        Assert.Equal("mp3", await Tool.AudioCodecAsync(Path.Combine(folder, "tone.mp3")));
    }
}
