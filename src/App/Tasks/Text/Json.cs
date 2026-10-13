using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Optimizer.Main.Process;

namespace Optimizer.Tasks.Text;

internal sealed class JsonFormat(Request request, bool sortKeys) : BatchTask(request)
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

        var formatted = sortKeys ? SortJson.Sort(node, Settings.Current.Text.JsonSort) : node;

        var output = OutputPath.Derive(path, sortKeys ? "_sorted" : "_pretty");
        using var working = new WorkingFile(output);
        File.WriteAllText(output, formatted.ToJsonString(WriteOptions), new UTF8Encoding(false));
        working.Keep();
    }, progress.Token);
}
