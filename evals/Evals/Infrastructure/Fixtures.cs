using System.Globalization;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using ImageMagick.Drawing;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Evals.Infrastructure;

internal static class Fixtures
{
    public const string OcrText = "FLECT OCR 2026";
    public const string SpeechText = "Hello world. This is a subtitle test for Flect.";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string Masters = Path.Combine(Paths.Work, "_fixtures");

    public static async Task<string> CopyAsync(string name, string folder, string? rename = null)
    {
        var master = await MasterAsync(name);
        var target = Path.Combine(folder, rename ?? name);
        File.Copy(master, target, overwrite: true);
        return target;
    }

    public static string? Bundled(string name)
    {
        var path = Path.Combine(Paths.Bundled, name);
        return File.Exists(path) ? path : null;
    }

    private static async Task<string> MasterAsync(string name)
    {
        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Masters);
            var path = Path.Combine(Masters, name);
            if (!File.Exists(path)) await CreateAsync(name, path);
            return path;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static Task CreateAsync(string name, string path) => name switch
    {
        "clip.mp4" => ClipAsync(path, 640, 360),
        "wide.mp4" => ClipAsync(path, 1280, 720),
        "portrait.mp4" => PortraitAsync(path),
        "tone.wav" => Tool.FfmpegAsync("-f", "lavfi", "-i", "sine=frequency=660:sample_rate=44100:duration=3",
                                       "-ac", "2", "-c:a", "pcm_s16le", path),
        "tone.mp3" => Tool.FfmpegAsync("-f", "lavfi", "-i", "sine=frequency=660:sample_rate=44100:duration=3",
                                       "-ac", "2", "-c:a", "libmp3lame", "-b:a", "128k", path),
        "photo.jpg" => Task.Run(() => Photo(path)),
        "noise.jpg" => Task.Run(() => Noise(path, MagickFormat.Jpeg)),
        "noise.png" => Task.Run(() => Noise(path, MagickFormat.Png32)),
        "alpha.png" => Task.Run(() => Alpha(path)),
        "green.png" => Task.Run(() => Green(path)),
        "animated.gif" => Task.Run(() => Animated(path)),
        "vector.svg" => File.WriteAllTextAsync(path,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\">" +
            "<rect width=\"120\" height=\"80\" fill=\"#1e90ff\"/>" +
            "<circle cx=\"60\" cy=\"40\" r=\"25\" fill=\"#ffd700\"/></svg>"),
        "alpha.pdf" => Task.Run(() => Pdf(path, "alpha")),
        "bravo.pdf" => Task.Run(() => Pdf(path, "bravo")),
        "ocr.png" => Sta(() => TextImage(path)),
        "speech.wav" => Task.Run(() => Speech(path)),
        _ => throw new ArgumentException($"Unknown fixture '{name}'.", nameof(name)),
    };

    private static Task ClipAsync(string path, int width, int height) =>
        Tool.FfmpegAsync(
            "-f", "lavfi", "-i", $"testsrc2=size={width}x{height}:rate=30:duration=4",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=4",
            "-c:v", "libx264", "-preset", "veryfast", "-g", "30", "-keyint_min", "30", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-shortest", "-movflags", "+faststart", path);

    private static async Task PortraitAsync(string path)
    {
        var wide = await MasterAsyncUnlocked("wide.mp4");
        await Tool.FfmpegAsync("-display_rotation", "90", "-i", wide, "-c", "copy", path);
    }

    private static async Task<string> MasterAsyncUnlocked(string name)
    {
        var path = Path.Combine(Masters, name);
        if (!File.Exists(path)) await CreateAsync(name, path);
        return path;
    }

    private static void Photo(string path)
    {
        using var image = new MagickImage(new MagickColor("#3366CC"), 400, 300);
        new Drawables().FillColor(MagickColors.Orange).Rectangle(40, 40, 360, 260).Draw(image);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "FlectCam");
        exif.SetValue(ExifTag.Software, "Flect Evals");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(35, 1), new Rational(41, 1), new Rational(0, 1)]);
        image.SetProfile(exif);
        image.Write(path, MagickFormat.Jpeg);
    }

    private static void Noise(string path, MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Gray, 1600, 1200);
        image.AddNoise(NoiseType.Random);
        image.Quality = 95;
        image.Write(path, format);
    }

    private static void Alpha(string path)
    {
        using var image = new MagickImage(MagickColors.Transparent, 300, 200);
        new Drawables().FillColor(MagickColors.Red).Rectangle(50, 50, 250, 150).Draw(image);
        image.Write(path, MagickFormat.Png32);
    }

    private static void Green(string path)
    {
        using var image = new MagickImage(new MagickColor("#00FF00"), 200, 200);
        new Drawables().FillColor(MagickColors.Red).Circle(100, 100, 100, 140).Draw(image);
        image.Write(path, MagickFormat.Png24);
    }

    private static void Animated(string path)
    {
        using var frames = new MagickImageCollection();
        foreach (var color in new[] { MagickColors.Red, MagickColors.Lime, MagickColors.Blue })
        {
            var frame = new MagickImage(color, 120, 80) { AnimationDelay = 20 };
            frames.Add(frame);
        }
        frames.Write(path, MagickFormat.Gif);
    }

    private static void Pdf(string path, string label)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (int number = 1; number <= 2; number++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"{label} page {number}", 18, new PdfPoint(72, 720), font);
        }
        File.WriteAllBytes(path, builder.Build());
    }

    private static void TextImage(string path)
    {
        var text = new FormattedText(OcrText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                     new Typeface("Arial"), 72, Brushes.Black, 1.0);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 900, 200));
            context.DrawText(text, new Point(40, 50));
        }
        var bitmap = new RenderTargetBitmap(900, 200, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Speech(string path)
    {
        using var synthesizer = new SpeechSynthesizer();
        Assert.SkipWhen(synthesizer.GetInstalledVoices().Count == 0, "No speech voice is installed.");
        synthesizer.SetOutputToWaveFile(path);
        synthesizer.Speak(SpeechText);
        synthesizer.SetOutputToNull();
    }

    public static Task Sta(Action action)
    {
        var completion = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
