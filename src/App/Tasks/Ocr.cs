using ImageMagick;
using Optimizer.Main;
using Optimizer.Gui;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Optimizer.Tasks;

internal sealed class ImageOcr(TaskRequest request) : BatchTask(request)
{
    private const uint ComfortableEdge = 1600;

    private static readonly System.Text.UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    public override string Title => "OCR";

    public override bool Configure()
    {
        OcrNoticeDialog.ShowOnce(AvailableLanguages());
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "Windows has no text recogniser installed for your languages. " +
                "Add the language under Settings > Time & Language > Language & Region.");

        using var bitmap = await ReadForRecognitionAsync(path, progress.Token);
        var result = await engine.RecognizeAsync(bitmap);
        var text = string.Join(Environment.NewLine, result.Lines.Select(Join));
        if (text.Trim().Length == 0)
        {
            progress.Warn($"{Path.GetFileName(path)} — no text found.");
            return;
        }

        var output = OutputPath.Derive(path, "_ocr", ".txt");
        using var working = new WorkingFile(output);
        await File.WriteAllTextAsync(output, text, Utf8Bom, progress.Token);
        working.Keep();
    }

    private static string Join(OcrLine line)
    {
        var text = new System.Text.StringBuilder();
        foreach (var word in line.Words)
        {
            if (word.Text.Length == 0) continue;
            if (text.Length > 0 && !(RunsTogether(text[^1]) && RunsTogether(word.Text[0])))
            {
                text.Append(' ');
            }
            text.Append(word.Text);
        }
        return text.ToString();
    }

    private static bool RunsTogether(char value) => value switch
    {
        >= '　' and <= '〿' => true,   // CJK punctuation
        >= '぀' and <= 'ヿ' => true,   // Hiragana and Katakana
        >= '㐀' and <= '䶿' => true,   // CJK extension A
        >= '一' and <= '鿿' => true,   // CJK unified ideographs
        >= '豈' and <= '﫿' => true,   // CJK compatibility ideographs
        >= '＀' and <= '￯' => true,   // Fullwidth forms
        _ => false,
    };

    private static async Task<SoftwareBitmap> ReadForRecognitionAsync(string path,
                                                                      CancellationToken token)
    {
        byte[] prepared = await Task.Run(() => Prepare(path), token);

        var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream);
        writer.WriteBytes(prepared);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }

    private static byte[] Prepare(string path)
    {
        using var frames = new MagickImageCollection(path);

        var image = frames.OrderByDescending(frame => (long)frame.Width * frame.Height).First();

        image.AutoOrient();

        uint longest = Math.Max(image.Width, image.Height);
        if (longest > 0 && longest < ComfortableEdge)
        {
            double factor = (double)ComfortableEdge / longest;
            image.Resize((uint)(image.Width * factor), (uint)(image.Height * factor));
        }
        else if (longest > OcrEngine.MaxImageDimension)
        {
            double factor = (double)OcrEngine.MaxImageDimension / longest;
            image.Resize((uint)(image.Width * factor), (uint)(image.Height * factor));
        }

        image.Format = MagickFormat.Bmp;
        return image.ToByteArray();
    }

    private static IReadOnlyList<string> AvailableLanguages()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages
                .Select(language => $"{language.DisplayName} ({language.LanguageTag})")
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
