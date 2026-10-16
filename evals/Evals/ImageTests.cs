using System.Globalization;
using Evals.Infrastructure;
using ImageMagick;
using Optimizer.Main.Ocr;

namespace Evals;

[Collection(Serial.Name)]
public sealed class ImageTests
{
    [Fact]
    public async Task ResizeHalvesTheImage()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-resize");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        await Harness.RunCleanAsync("image.resize?scale=50", input);

        using var output = new MagickImage(Path.Combine(folder, "alpha_resized.png"));
        Assert.Equal((150u, 100u), (output.Width, output.Height));
    }

    [Fact]
    public async Task ResizeReadsDecimalPresetsWhateverTheCulture()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-resize-decimal");
        var input = await Fixtures.CopyAsync("alpha.png", folder);
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            await Harness.RunCleanAsync("image.resize?scale=12.5", input);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        using var output = new MagickImage(Path.Combine(folder, "alpha_resized.png"));
        Assert.InRange(output.Width, 30u, 50u);
    }

    [Fact]
    public async Task RotateNinetySwapsTheSides()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-rotate");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        await Harness.RunCleanAsync("image.rotate?deg=90", input);

        using var output = new MagickImage(Path.Combine(folder, "alpha_rotated.png"));
        Assert.Equal((200u, 300u), (output.Width, output.Height));
    }

    [Fact]
    public async Task RotateAtAnAngleLeavesTransparentCorners()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-rotate-angle");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        await Harness.RunCleanAsync("image.rotate?deg=45", input);

        using var output = new MagickImage(Path.Combine(folder, "alpha_rotated.png"));
        Assert.True(output.Width > 300 && output.Height > 200);
        using var pixels = output.GetPixels();
        Assert.Equal(0, pixels.GetPixel(0, 0).ToColor()!.A);
    }

    [Theory]
    [InlineData("JPG", MagickFormat.Jpeg)]
    [InlineData("WEBP", MagickFormat.WebP)]
    [InlineData("ICO", MagickFormat.Ico)]
    [InlineData("GIF", MagickFormat.Gif)]
    public async Task ConvertWritesTheTargetFormat(string target, MagickFormat expected)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-convert");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        await Harness.RunCleanAsync($"image.convert?to={target}", input);

        var output = Path.Combine(folder, "alpha." + target.ToLowerInvariant());
        var info = new MagickImageInfo(output);
        Assert.Equal(expected, info.Format);
    }

    [Fact]
    public async Task ConvertToSvgEmbedsThePicture()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-svg");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        await Harness.RunCleanAsync("image.convert?to=SVG", input);

        var svg = await File.ReadAllTextAsync(Path.Combine(folder, "alpha.svg"), TestContext.Current.CancellationToken);
        Assert.Contains("<svg", svg);
        Assert.Contains("data:image/png;base64,", svg);
    }

    [Fact]
    public async Task ConvertReadsSvg()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-from-svg");
        var input = await Fixtures.CopyAsync("vector.svg", folder);

        await Harness.RunCleanAsync("image.convert?to=PNG", input);

        using var output = new MagickImage(Path.Combine(folder, "vector.png"));
        Assert.Equal((120u, 80u), (output.Width, output.Height));
    }

    [Fact]
    public async Task ConvertToHeicEitherWritesOrExplainsTheMissingEncoder()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-heic");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        var progress = await Harness.RunAsync("image.convert?to=HEIC", input);

        var written = File.Exists(Path.Combine(folder, "alpha.heic"));
        Assert.True(written || progress.Errors.Any(error => error.Contains("HEVC")),
                    "Neither a HEIC file nor the HEVC explanation: " + string.Join(" | ", progress.Errors));
    }

    [Fact]
    public async Task ConvertReadsHeicWhenASampleIsProvided()
    {
        var sample = Fixtures.Bundled("sample.heic");
        Assert.SkipWhen(sample is null, "Place evals/fixtures/sample.heic to cover HEIC input.");
        TestSettings.Use();
        var folder = Paths.NewFolder("image-heic-in");
        var input = Path.Combine(folder, "sample.heic");
        File.Copy(sample!, input);

        await Harness.RunCleanAsync("image.convert?to=JPG", input);

        Assert.True(File.Exists(Path.Combine(folder, "sample.jpg")));
    }

    [Fact]
    public async Task AnimatedGifKeepsItsFramesWhenResized()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-gif");
        var input = await Fixtures.CopyAsync("animated.gif", folder);

        await Harness.RunCleanAsync("image.resize?scale=50", input);

        using var frames = new MagickImageCollection(Path.Combine(folder, "animated_resized.gif"));
        Assert.Equal(3, frames.Count);
        Assert.Equal(60u, frames[0].Width);
    }

    [Theory]
    [InlineData("noise.jpg")]
    [InlineData("noise.png")]
    public async Task CompressReachesTheTargetOrSaysWhy(string fixture)
    {
        TestSettings.Use();
        const long target = 300 * 1024;
        var folder = Paths.NewFolder("image-compress");
        var input = await Fixtures.CopyAsync(fixture, folder);

        var progress = await Harness.RunCleanAsync($"image.compress?bytes={target}", input);

        var output = Directory.GetFiles(folder, Path.GetFileNameWithoutExtension(fixture) + "_compressed.*").Single();
        Assert.True(new FileInfo(output).Length <= target || progress.Warnings.Count > 0,
                    $"{new FileInfo(output).Length} bytes and no warning");
    }

    [Fact]
    public async Task CompressSkipsFilesAlreadyUnderTheTarget()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-compress-small");
        var input = await Fixtures.CopyAsync("alpha.png", folder);

        var progress = await Harness.RunCleanAsync("image.compress?bytes=10485760", input);

        Assert.Empty(Directory.GetFiles(folder, "alpha_compressed.*"));
        Assert.NotEmpty(progress.Infos);
    }

    [Fact]
    public async Task StripMetadataRemovesGpsAndCameraDetails()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("image-strip");
        var input = await Fixtures.CopyAsync("photo.jpg", folder);
        using (var original = new MagickImage(input))
        {
            Assert.NotNull(original.GetExifProfile());
        }

        await Harness.RunCleanAsync("image.strip", input);

        using var output = new MagickImage(Path.Combine(folder, "photo_clean.jpg"));
        var exif = output.GetExifProfile();
        Assert.True(exif is null || exif.Values.All(value => !value.Tag.ToString().StartsWith("GPS", StringComparison.Ordinal)));
        Assert.True(exif?.GetValue(ExifTag.Make) is null);
    }

    [Fact]
    public async Task ChromaKeyMakesTheKeyColourTransparent()
    {
        TestSettings.Use(settings => settings.Image.BackgroundMode = BackgroundRemovalMode.ChromaKeyOnly);
        var folder = Paths.NewFolder("image-chromakey");
        var input = await Fixtures.CopyAsync("green.png", folder);

        await Harness.RunCleanAsync("image.chromakey", input);

        using var output = new MagickImage(Path.Combine(folder, "green_keyed.png"));
        using var pixels = output.GetPixels();
        Assert.Equal(0, pixels.GetPixel(5, 5).ToColor()!.A);
        Assert.Equal(255, pixels.GetPixel(100, 100).ToColor()!.A);
    }

    [Fact]
    public async Task OcrReadsRenderedText()
    {
        Assert.SkipWhen(OcrLanguages.Installed().Count == 0, "Windows has no OCR recognizer installed.");
        TestSettings.Use(settings => settings.Ocr.Output = OcrOutput.TextFile);
        var folder = Paths.NewFolder("image-ocr");
        var input = await Fixtures.CopyAsync("ocr.png", folder);

        await Harness.RunCleanAsync("image.ocr", input);

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "ocr_ocr.txt"), TestContext.Current.CancellationToken);
        Assert.Contains("FLECT", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", text);
    }
}
