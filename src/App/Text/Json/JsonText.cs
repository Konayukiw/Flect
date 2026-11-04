using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flect.Text.Json;

internal sealed class JsonText(Request request, bool sortKeys) : Flect.Dispatch.Batch(request)
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public override string Title => Loc.T(sortKeys ? "menu.json.sort" : "menu.json.pretty");

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var node = JsonNode.Parse(File.ReadAllText(path), nodeOptions: null, ParseOptions)
            ?? throw new InvalidOperationException(Loc.T("msg.emptyFile"));

        var formatted = sortKeys ? JsonSort.Sort(node, Preferences.Current.Text.JsonSort) : node;

        var output = Flect.Files.OutputName.Derive(path, sortKeys ? "_sorted" : "_pretty");
        using var working = new Flect.Files.TempFile(output);
        File.WriteAllText(output, formatted.ToJsonString(WriteOptions), new UTF8Encoding(false));
        working.Keep();
    }, progress.Token);
}
