using System.Text;

namespace Flect.Text;

internal sealed class LineEndings(Request request) : Flect.Dispatch.Batch(request)
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
        var text = TextRead.Read(path, out var encoding);
        var ending = _target == "LF" ? "\n" : "\r\n";
        var normalised = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var converted = ending == "\n" ? normalised : normalised.Replace("\n", ending);

        var output = Flect.Files.OutputName.Derive(path, _target == "LF" ? "_lf" : "_crlf");
        using var working = new Flect.Files.TempFile(output);
        File.WriteAllText(output, converted, encoding);
        working.Keep();
    }, progress.Token);
}
