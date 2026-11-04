namespace Flect.Text.Json;

internal sealed class JsonSortSettings
{
    public JsonKeyOrder Comparison { get; set; } = JsonKeyOrder.Ordinal;
    public bool Descending { get; set; }
    public bool Recursive { get; set; } = true;
    public bool SortPrimitiveArrays { get; set; }
    public string PinnedKeys { get; set; } = string.Empty;
}
