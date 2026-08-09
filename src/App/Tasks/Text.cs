using System.Text;
using Optimizer.Main;
using UtfUnknown;

namespace Optimizer.Tasks;

internal sealed class TextEncode(TaskRequest request) : BatchTask(request)
{
    private const int ShiftJisCodePage = 932;

    private string _target = "UTF8";

    public override string Title => "Format";

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
            File.WriteAllText(output, text, Encoding.GetEncoding(ShiftJisCodePage));
            progress.Warn($"{Path.GetFileName(path)} — Some characters have no Shift_JIS " +
                          "equivalent and were replaced.");
        }
        working.Keep();
    }, progress.Token);

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

    public override string Title => "Line Endings";

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
