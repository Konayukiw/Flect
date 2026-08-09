using System.Text;
using Optimizer.Main;

namespace Optimizer.Tasks;

internal sealed class TextEncode(TaskRequest request) : BatchTask(request)
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
        var text = Text.Read(path);
        var output = OutputPath.Derive(path, Suffix());
        using var working = new WorkingFile(output);

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
            OutputPath.SafeDelete(output);
            WriteWithFallback(output, text, path, progress);
        }
        working.Keep();
    }, progress.Token);

    private static void WriteWithFallback(string output, string text, string source,
                                          ITaskProgress progress)
    {
        var settings = Settings.Current.Text;
        if (settings.SjisFallback == SjisFallback.Fail)
        {
            throw new InvalidOperationException(Loc.T("msg.sjisFailed"));
        }

        var encoding = settings.SjisFallback == SjisFallback.Substitute
            ? Encoding.GetEncoding(ShiftJisCodePage,
                                   new EncoderReplacementFallback(settings.SjisSubstitute),
                                   DecoderFallback.ReplacementFallback)
            : Encoding.GetEncoding(ShiftJisCodePage);

        File.WriteAllText(output, text, encoding);

        if (settings.SjisFallback == SjisFallback.ReplaceAndWarn)
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

internal sealed class TextLineEndings(TaskRequest request) : BatchTask(request)
{
    private string _target = "CRLF";

    public override string Title => Loc.T("menu.text.lineEndings");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "CRLF").ToUpperInvariant();
        return true;
    }

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var text = Text.Read(path, out var encoding);
        var ending = _target == "LF" ? "\n" : "\r\n";
        var normalised = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var converted = ending == "\n" ? normalised : normalised.Replace("\n", ending);

        var output = OutputPath.Derive(path, _target == "LF" ? "_lf" : "_crlf");
        using var working = new WorkingFile(output);
        File.WriteAllText(output, converted, encoding);
        working.Keep();
    }, progress.Token);
}
