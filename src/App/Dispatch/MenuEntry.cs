namespace Flect.Dispatch;

internal sealed record MenuEntry(string Id, string LabelKey, string Label,
                                 IReadOnlyList<MenuEntry> Children)
{
    public static MenuEntry Leaf(string id, string label, string? labelKey = null) =>
        new(id, labelKey ?? "menu." + id, label, []);

    public static MenuEntry Group(string id, string label, params MenuEntry[] children) =>
        new(id, "menu." + id, label, children);
}
