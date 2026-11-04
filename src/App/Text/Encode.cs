using System.Text;

namespace Flect.Text;

internal sealed class Encode(Request request) : Flect.Dispatch.Batch(request)
{
    private const int ShiftJisCodePage = 932;

    private string _target = "UTF8";

    public override string Title => Loc.T("menu.text.encode");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "UTF8").ToUpperInvariant();
        return true;
    }

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var text = TextRead.Read(path);
        var output = Flect.Files.OutputName.Derive(path, Suffix());
        using var working = new Flect.Files.TempFile(output);

        if (_target != "SJIS")
        {
            File.WriteAllText(output, text, Target());
            working.Keep();
            return;
        }

        try
        {
            File.WriteAllText(output, text,
                Encoding.GetEncoding(ShiftJisCodePage, EncoderFallback.ExceptionFallback,
                                     DecoderFallback.ReplacementFallback));
        }
        catch (EncoderFallbackException)
        {
            Flect.Files.OutputName.SafeDelete(output);
            WriteWithFallback(output, text, path, progress);
        }
        working.Keep();
    }, progress.Token);

    private static void WriteWithFallback(string output, string text, string source,
                                          ITaskProgress progress)
    {
        var settings = Preferences.Current.Text;
        if (settings.SjisFallback == ShiftJISFallback.Fail)
        {
            throw new InvalidOperationException(Loc.T("msg.sjisFailed"));
        }

        var encoding = settings.SjisFallback == ShiftJISFallback.Substitute
            ? Encoding.GetEncoding(ShiftJisCodePage,
                                   new EncoderReplacementFallback(settings.SjisSubstitute),
                                   DecoderFallback.ReplacementFallback)
            : Encoding.GetEncoding(ShiftJisCodePage);

        File.WriteAllText(output, text, encoding);

        if (settings.SjisFallback == ShiftJISFallback.ReplaceAndWarn)
        {
            progress.Warn(Loc.F("msg.sjisReplaced", Path.GetFileName(source)));
        }
    }

    private Encoding Target() => _target switch
    {
        "UTF8BOM" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        "UTF16" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        "SJIS" => Encoding.GetEncoding(ShiftJisCodePage),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    private string Suffix() => _target switch
    {
        "UTF8BOM" => "_utf8bom",
        "UTF16" => "_utf16",
        "SJIS" => "_sjis",
        _ => "_utf8",
    };
}
