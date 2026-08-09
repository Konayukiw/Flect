using System.Text;
using System.Windows;
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

    private readonly StringBuilder _collected = new();

    private OcrSettings _settings = new();
    private string? _summary;

    public override string Title => Loc.T("menu.image.ocr");

    public override string? Summary => _summary;

    public override bool Configure()
    {
        _settings = Settings.Current.Ocr;
        OcrNotice.ShowOnce(OcrLanguages.Installed().Select(language => language.Display).ToList());
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var engine = OcrLanguages.Create(_settings.Language)
            ?? throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(_settings.Language)
                    ? Loc.T("msg.ocrNoEngine")
                    : Loc.F("msg.ocrNoLanguage", _settings.Language));

        using var bitmap = await ReadForRecognitionAsync(path, progress.Token);
        var result = await engine.RecognizeAsync(bitmap);
        var text = string.Join(Environment.NewLine, result.Lines.Select(Join));

        if (text.Trim().Length == 0)
        {
            progress.Warn(Loc.F("msg.noTextFound", Path.GetFileName(path)));
            return;
        }

        if (_settings.Output != OcrOutput.TextFile)
        {
            Collect(path, text);
            return;
        }

        var output = OutputPath.Derive(path, "_ocr", ".txt");
        using var working = new WorkingFile(output);
        await File.WriteAllTextAsync(output, text, FileEncoding(_settings.Encoding), progress.Token);
        working.Keep();
    }

    public override void Present()
    {
        if (_settings.Output == OcrOutput.TextFile || _collected.Length == 0) return;

        var text = _collected.ToString();
        if (_settings.Output == OcrOutput.Clipboard)
        {
            try
            {
                Clipboard.SetText(text);
                _summary = Loc.T("msg.ocrCopied");
            }
            catch (Exception)
            {
                _summary = Loc.T("common.copyFailed");
            }
        }

        new TextResult(Title, text).Show();
    }

    private void Collect(string path, string text)
    {
        if (Request.Paths.Count > 1)
        {
            if (_collected.Length > 0) _collected.AppendLine().AppendLine();
            _collected.AppendLine($"== {Path.GetFileName(path)} ==");
        }
        _collected.AppendLine(text);
    }

    private static Encoding FileEncoding(TextEncodingChoice choice) => choice switch
    {
        TextEncodingChoice.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        TextEncodingChoice.Utf16 =>
            new System.Text.UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        TextEncodingChoice.ShiftJis => Encoding.GetEncoding(932),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
    };

    private static string Join(OcrLine line)
    {
        var text = new StringBuilder();
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
        >= '豈' and <= '﫿' => true,   // CJK compatibility ideographs
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
}
