using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Optimizer.Main;

namespace Optimizer.Tasks;

internal sealed class JsonFormat(TaskRequest request, bool sortKeys) : BatchTask(request)
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

    public override string Title => sortKeys ? "Sort Keys" : "Pretty Print";

    protected override Task ProcessAsync(string path, ITaskProgress progress) => Task.Run(() =>
    {
        var node = JsonNode.Parse(File.ReadAllText(path), nodeOptions: null, ParseOptions)
            ?? throw new InvalidOperationException("This file is empty.");

        var formatted = sortKeys ? Sorted(node) ?? node : node;

        var output = OutputPath.Derive(path, sortKeys ? "_sorted" : "_pretty");
        using var working = new WorkingFile(output);
        File.WriteAllText(output, formatted.ToJsonString(WriteOptions), new UTF8Encoding(false));
        working.Keep();
    }, progress.Token);

    private static JsonNode? Sorted(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject source:
            {
                var result = new JsonObject();
                foreach (var member in source.OrderBy(member => member.Key, StringComparer.Ordinal))
                {
                    result[member.Key] = Sorted(member.Value?.DeepClone());
                }
                return result;
            }

            case JsonArray source:
            {
                var result = new JsonArray();
                foreach (var item in source) result.Add(Sorted(item?.DeepClone()));
                return result;
            }

            default:
                return node?.DeepClone();
        }
    }
}
