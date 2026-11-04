using System.Text.Json.Nodes;

namespace Flect.Text.Json;

internal static class JsonSort
{
    public static JsonNode Sort(JsonNode node, JsonSortSettings settings)
    {
        var pinned = Flect.Folder.Exclusion.Split(settings.PinnedKeys);
        return Walk(node.DeepClone(), settings, pinned, depth: 0) ?? node;
    }

    private static JsonNode? Walk(JsonNode? node, JsonSortSettings settings,
                                  IReadOnlyList<string> pinned, int depth)
    {
        switch (node)
        {
            case JsonObject source:
            {
                if (depth > 0 && !settings.Recursive) return source;

                var result = new JsonObject();
                foreach (var member in Order(source, settings, pinned))
                {
                    result[member.Key] = Walk(member.Value?.DeepClone(), settings, pinned, depth + 1);
                }
                return result;
            }

            case JsonArray source:
            {
                if (depth > 0 && !settings.Recursive) return source;

                var items = source.Select(item => Walk(item?.DeepClone(), settings, pinned, depth + 1))
                                  .ToList();

                if (settings.SortPrimitiveArrays && items.All(item => item is JsonValue))
                {
                    items = Arrange(items, settings, item => item?.ToJsonString() ?? string.Empty);
                }
                return new JsonArray([.. items]);
            }

            default:
                return node;
        }
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> Order(
        JsonObject source, JsonSortSettings settings, IReadOnlyList<string> pinned)
    {
        var members = source.ToList();

        var head = pinned
            .Select(key => members.FirstOrDefault(
                member => string.Equals(member.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Where(member => member.Key is not null)
            .ToList();

        var taken = head.Select(member => member.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tail = members.Where(member => !taken.Contains(member.Key)).ToList();

        return head.Concat(Arrange(tail, settings, member => member.Key));
    }

    private static List<T> Arrange<T>(IEnumerable<T> items, JsonSortSettings settings,
                                      Func<T, string> key)
    {
        var comparer = Comparer(settings.Comparison);
        var ordered = settings.Descending
            ? items.OrderByDescending(key, comparer)
            : items.OrderBy(key, comparer);
        return ordered.ToList();
    }

    private static IComparer<string> Comparer(JsonKeyOrder order) => order switch
    {
        JsonKeyOrder.IgnoreCase => StringComparer.OrdinalIgnoreCase,
        JsonKeyOrder.Natural => NaturalSort.Instance,
        _ => StringComparer.Ordinal,
    };
}
