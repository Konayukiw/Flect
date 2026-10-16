using System.Text.Json.Nodes;

namespace Optimizer.Main.Text;

internal static class SortJson
{
    public static JsonNode Sort(JsonNode node, JsonSortSettings settings)
    {
        var pinned = ExclusionFilter.Split(settings.PinnedKeys);
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
        JsonKeyOrder.Natural => NaturalComparer.Instance,
        _ => StringComparer.Ordinal,
    };
}

internal sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? left, string? right)
    {
        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;

        int a = 0;
        int b = 0;

        while (a < left.Length && b < right.Length)
        {
            if (char.IsDigit(left[a]) && char.IsDigit(right[b]))
            {
                int startA = a;
                int startB = b;
                while (a < left.Length && char.IsDigit(left[a])) a++;
                while (b < right.Length && char.IsDigit(right[b])) b++;

                var numberA = left.AsSpan(startA, a - startA).TrimStart('0');
                var numberB = right.AsSpan(startB, b - startB).TrimStart('0');

                if (numberA.Length != numberB.Length) return numberA.Length - numberB.Length;

                int digits = numberA.SequenceCompareTo(numberB);
                if (digits != 0) return digits;
                continue;
            }

            int letters = left[a].CompareTo(right[b]);
            if (letters != 0) return letters;
            a++;
            b++;
        }

        return (left.Length - a) - (right.Length - b);
    }
}
