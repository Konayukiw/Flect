using Flect.I18n;

namespace Evals.Inspect;

[Collection(Serial.Name)]
public sealed class LinkTests
{
    private const long MiB = 1024L * 1024L;

    public LinkTests() => Loc.Use(AppLanguage.English);

    [Fact]
    public void FactsKeepTheKeyUsedToAttachAnAction()
    {
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.path", @"C:\clip.png");
        bag.Add("dialog.fileAnalyze.size", "1 B");

        Assert.Equal("dialog.fileAnalyze.path", bag.Rows[0].Key);
        Assert.Equal("Path", bag.Rows[0].Label);
        Assert.Equal("dialog.fileAnalyze.size", bag.Rows[1].Key);
    }

    [Fact]
    public void CopyTargetsArePathHashNeighboursOnly()
    {
        Assert.True(Flect.Inspect.Suggest.Suggest.CanCopy("dialog.fileAnalyze.path"));
        Assert.True(Flect.Inspect.Suggest.Suggest.CanCopy("dialog.fileAnalyze.linkTarget"));
        Assert.True(Flect.Inspect.Suggest.Suggest.CanCopy("dialog.fileAnalyze.zoneUrl"));
        Assert.True(Flect.Inspect.Suggest.Suggest.CanCopy("dialog.fileAnalyze.target"));
        Assert.False(Flect.Inspect.Suggest.Suggest.CanCopy("dialog.fileAnalyze.size"));
        Assert.False(Flect.Inspect.Suggest.Suggest.CanCopy("dialog.fileAnalyze.contents"));
        Assert.False(Flect.Inspect.Suggest.Suggest.CanCopy(null));
    }

    [Fact]
    public void SmallImageKeepsCustomCompressAndSkipsLargerPresets()
    {
        var actions = Actions("clip.png", 100);

        Assert.Null(actions.Link("dialog.fileAnalyze.size"));
        var compress = actions.Link("dialog.fileAnalyze.sizeOnDisk");
        Assert.Equal("Compress", compress!.Label);
        Assert.False(compress.Direct);
        Assert.Equal(["image.compress?bytes=custom"], Verbs(compress));

        var resize = actions.Link("dialog.fileAnalyze.dimensions");
        Assert.Equal(["image.resize?scale=50", "image.resize?scale=25", "image.resize?scale=custom"],
                     Verbs(resize));
        Assert.Equal(["50%", "25%", "Custom"], Labels(resize));
        Assert.Null(actions.Link("dialog.fileAnalyze.resolution"));
        Assert.Null(actions.Link("dialog.fileAnalyze.duration"));
    }

    [Fact]
    public void ImageCompressPresetAppearsOnlyWhenTheFileIsLarger()
    {
        var atLimit = Actions("clip.png", MiB);
        Assert.Equal(["image.compress?bytes=custom"], Verbs(atLimit.Link("dialog.fileAnalyze.sizeOnDisk")));

        var above = Actions("clip.png", MiB + 1);
        Assert.Equal(["image.compress?bytes=1048576", "image.compress?bytes=custom"],
                     Verbs(above.Link("dialog.fileAnalyze.sizeOnDisk")));
        Assert.Equal("1 MB", above.Link("dialog.fileAnalyze.sizeOnDisk")!.Commands[0].Label);
    }

    [Fact]
    public void ResizeUsesTheConfiguredPercents()
    {
        var image = new ImagePresets { ResizeA = 80, ResizeB = 12.5 };
        var actions = new Flect.Inspect.Suggest.Suggest("clip.png", 10, new GeneralSettings(), image, new VideoPresets());

        Assert.Equal(["80%", "12.5%", "Custom"], Labels(actions.Link("dialog.fileAnalyze.dimensions")));
        Assert.Equal(["image.resize?scale=80", "image.resize?scale=12.5", "image.resize?scale=custom"],
                     Verbs(actions.Link("dialog.fileAnalyze.dimensions")));
    }

    [Fact]
    public void ConvertSkipsTheExtensionAndAnimatedGifHasNoConvert()
    {
        var jpeg = Actions("photo.jpeg", 10);
        Assert.DoesNotContain("image.convert?to=JPG", Verbs(jpeg.Link("dialog.fileAnalyze.contents")));
        Assert.Contains("image.convert?to=PNG", Verbs(jpeg.Link("dialog.fileAnalyze.contents")));
        Assert.Equal("Convert", jpeg.Link("dialog.fileAnalyze.contents")!.Label);

        var webp = Actions("clip.webp", 10);
        Assert.DoesNotContain("image.convert?to=WEBP", Verbs(webp.Link("dialog.fileAnalyze.contents")));

        var heif = Actions("clip.heif", 10);
        Assert.DoesNotContain("image.convert?to=HEIC", Verbs(heif.Link("dialog.fileAnalyze.contents")));

        var folder = Paths.NewFolder("gif-links");
        var still = Path.Combine(folder, "still.gif");
        var moving = Path.Combine(folder, "moving.gif");
        File.WriteAllBytes(still, StillGif());
        File.WriteAllBytes(moving, NetscapeGif());

        var stillActions = Actions(still, 10);
        Assert.DoesNotContain("image.convert?to=GIF", Verbs(stillActions.Link("dialog.fileAnalyze.contents")));
        Assert.Contains("image.convert?to=PNG", Verbs(stillActions.Link("dialog.fileAnalyze.contents")));
        Assert.NotNull(stillActions.Link("dialog.fileAnalyze.dimensions"));
        Assert.Null(stillActions.Link("dialog.fileAnalyze.duration"));

        var movingActions = Actions(moving, 10);
        Assert.Null(movingActions.Link("dialog.fileAnalyze.contents"));
        Assert.NotNull(movingActions.Link("dialog.fileAnalyze.dimensions"));
        Assert.NotNull(movingActions.Link("dialog.fileAnalyze.sizeOnDisk"));
    }

    [Fact]
    public void VideoLinksFollowTheShellMenu()
    {
        var small = Actions("clip.mp4", 20 * MiB);
        Assert.DoesNotContain(Verbs(small.Link("dialog.fileAnalyze.sizeOnDisk")),
                              verb => verb.Contains("discord", StringComparison.Ordinal));
        Assert.Equal(["video.compress?bytes=5242880", "video.compress?bytes=custom"],
                     Verbs(small.Link("dialog.fileAnalyze.sizeOnDisk")));

        var large = Actions("clip.mp4", 20 * MiB + 1);
        Assert.Equal("Discord (20 MB)", large.Link("dialog.fileAnalyze.sizeOnDisk")!.Commands[0].Label);
        Assert.Equal("video.compress?preset=discord",
                     large.Link("dialog.fileAnalyze.sizeOnDisk")!.Commands[0].Verb);
        Assert.Contains("video.compress?bytes=5242880",
                        Verbs(large.Link("dialog.fileAnalyze.sizeOnDisk")));
        Assert.DoesNotContain("video.compress?bytes=52428800",
                              Verbs(large.Link("dialog.fileAnalyze.sizeOnDisk")));

        var convert = large.Link("dialog.fileAnalyze.contents");
        Assert.DoesNotContain("video.convert?to=MP4", Verbs(convert));
        Assert.Equal("video.convert?to=GIF", Verbs(convert)[^1]);
        Assert.Equal("Trim", large.Link("dialog.fileAnalyze.duration")!.Label);
        Assert.True(large.Link("dialog.fileAnalyze.duration")!.Direct);
        Assert.Equal(["video.trim"], Verbs(large.Link("dialog.fileAnalyze.duration")));
        Assert.Equal(["video.resize?scale=50", "video.resize?scale=25", "video.resize?scale=custom"],
                     Verbs(large.Link("dialog.fileAnalyze.resolution")));
        Assert.Null(large.Link("dialog.fileAnalyze.dimensions"));
    }

    [Fact]
    public void M4aIsTrimmedAsVideoAndAudioUsesItsOwnTrim()
    {
        var m4a = Actions("song.m4a", 10);
        Assert.Equal(["video.trim"], Verbs(m4a.Link("dialog.fileAnalyze.duration")));
        Assert.Contains("video.convert?to=MP4", Verbs(m4a.Link("dialog.fileAnalyze.contents")));
        Assert.DoesNotContain("video.convert?to=M4A", Verbs(m4a.Link("dialog.fileAnalyze.contents")));

        var mp3 = Actions("song.mp3", 10);
        Assert.Equal("Trim", mp3.Link("dialog.fileAnalyze.duration")!.Label);
        Assert.Equal(["audio.trim"], Verbs(mp3.Link("dialog.fileAnalyze.duration")));
        Assert.DoesNotContain("audio.convert?to=MP3", Verbs(mp3.Link("dialog.fileAnalyze.contents")));
        Assert.Contains("audio.convert?to=WAV", Verbs(mp3.Link("dialog.fileAnalyze.contents")));
        Assert.Null(mp3.Link("dialog.fileAnalyze.sizeOnDisk"));
        Assert.Null(mp3.Link("dialog.fileAnalyze.resolution"));
    }

    [Fact]
    public void TextPdfAndArchiveUseTheMatchingCommand()
    {
        var json = Actions("note.json", 10);
        Assert.Equal(["text.convert?to=TXT", "text.convert?to=CSV", "text.convert?to=XML"],
                     Verbs(json.Link("dialog.fileAnalyze.contents")));
        Assert.Equal(
            ["text.encode?to=UTF8", "text.encode?to=UTF8BOM", "text.encode?to=UTF16", "text.encode?to=SJIS"],
            Verbs(json.Link("dialog.fileAnalyze.encoding")));
        Assert.Equal(["to Shift_JIS"], json.Link("dialog.fileAnalyze.encoding")!.Commands
                         .Where(command => command.Verb.EndsWith("SJIS", StringComparison.Ordinal))
                         .Select(command => command.Label));
        Assert.Equal(["text.lineEndings?to=CRLF", "text.lineEndings?to=LF"],
                     Verbs(json.Link("dialog.fileAnalyze.newlines")));

        var plain = Actions("note.txt", 10);
        Assert.Null(plain.Link("dialog.fileAnalyze.contents"));
        Assert.NotNull(plain.Link("dialog.fileAnalyze.encoding"));

        var csv = Actions("sheet.csv", 10);
        Assert.Equal(["text.convert?to=TXT", "text.convert?to=JSON", "text.convert?to=XML",
                      "text.convert?to=HTML", "text.convert?to=MD"],
                     Verbs(csv.Link("dialog.fileAnalyze.contents")));

        var code = Actions("Program.cs", 10);
        Assert.Equal(["text.convert?to=TXT"], Verbs(code.Link("dialog.fileAnalyze.contents")));

        var pdf = Actions("book.pdf", 10);
        Assert.Equal(["pdf.convert?to=PNG", "pdf.convert?to=JPG", "pdf.convert?to=TXT"],
                     Verbs(pdf.Link("dialog.fileAnalyze.contents")));
        Assert.Null(pdf.Link("dialog.fileAnalyze.sizeOnDisk"));

        var zip = Actions("pack.zip", 10);
        Assert.Equal("Extract", zip.Link("dialog.fileAnalyze.contents")!.Label);
        Assert.True(zip.Link("dialog.fileAnalyze.contents")!.Direct);
        Assert.Equal(["archive.extract"], Verbs(zip.Link("dialog.fileAnalyze.contents")));
        Assert.Equal(["archive.extract"], Verbs(Actions("bundle.tar.gz", 10).Link("dialog.fileAnalyze.contents")));

        Assert.Null(Actions("tool.jar", 10).Link("dialog.fileAnalyze.contents"));
        Assert.Null(Actions("pack.gz", 10).Link("dialog.fileAnalyze.contents"));
        Assert.Null(Actions("song.flac", 10).Link("dialog.fileAnalyze.duration"));
        Assert.Null(Actions("clip.png", 10).Link("dialog.fileAnalyze.dpi"));
        Assert.Null(Actions("clip.png", 10).Link("dialog.fileAnalyze.orientation"));
    }

    [Fact]
    public void OddBytePresetsUseTheShellSizeLabel()
    {
        var image = new ImagePresets { CompressA = 1536 * 1024 };
        var actions = new Flect.Inspect.Suggest.Suggest("clip.png", 2 * MiB, new GeneralSettings(), image, new VideoPresets());
        Assert.Equal("1.5 MB", actions.Link("dialog.fileAnalyze.sizeOnDisk")!.Commands[0].Label);
        Assert.Equal("image.compress?bytes=1572864",
                     actions.Link("dialog.fileAnalyze.sizeOnDisk")!.Commands[0].Verb);
    }

    [Fact]
    public void HiddenMenuItemsRemoveTheLinkOrTheChild()
    {
        var group = Actions("clip.png", 20 * MiB, "image.compress");
        Assert.Null(group.Link("dialog.fileAnalyze.sizeOnDisk"));
        Assert.NotNull(group.Link("dialog.fileAnalyze.dimensions"));

        var child = Actions("clip.png", 20 * MiB, "image.compress.p1", "image.resize.custom");
        Assert.DoesNotContain("image.compress?bytes=10485760",
                              Verbs(child.Link("dialog.fileAnalyze.sizeOnDisk")));
        Assert.Contains("image.compress?bytes=5242880",
                        Verbs(child.Link("dialog.fileAnalyze.sizeOnDisk")));
        Assert.DoesNotContain("image.resize?scale=custom",
                              Verbs(child.Link("dialog.fileAnalyze.dimensions")));

        var empty = Actions("note.json", 10, "text.convert.TXT", "text.convert.CSV", "text.convert.XML");
        Assert.Null(empty.Link("dialog.fileAnalyze.contents"));

        var trim = Actions("clip.mp4", 10, "video.trim");
        Assert.Null(trim.Link("dialog.fileAnalyze.duration"));

        var archive = Actions("pack.zip", 10, "archive.extract");
        Assert.Null(archive.Link("dialog.fileAnalyze.contents"));
    }

    private static Flect.Inspect.Suggest.Suggest Actions(string name, long length, params string[] hidden) =>
        new(name, length, new GeneralSettings { HiddenMenuItems = [..hidden] },
            new ImagePresets(), new VideoPresets());

    private static List<string> Verbs(Flect.Inspect.Suggest.SuggestLink? link) =>
        link is null ? [] : link.Commands.Select(command => command.Verb).ToList();

    private static List<string> Labels(Flect.Inspect.Suggest.SuggestLink? link) =>
        link is null ? [] : link.Commands.Select(command => command.Label).ToList();

    private static byte[] StillGif() =>
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B,
    ];

    private static byte[] NetscapeGif() =>
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x21, 0xFF, 0x0B, 0x4E, 0x45, 0x54, 0x53,
        0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, 0x03, 0x01, 0x00, 0x00, 0x00, 0x2C,
        0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01,
        0x00, 0x3B,
    ];
}
