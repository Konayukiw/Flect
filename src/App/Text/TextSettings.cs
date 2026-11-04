namespace Flect.Text;

internal sealed class TextSettings
{
    public ShiftJISFallback SjisFallback { get; set; } = ShiftJISFallback.ReplaceAndWarn;
    public string SjisSubstitute { get; set; } = "?";
    public JsonSortSettings JsonSort { get; set; } = new();
}
