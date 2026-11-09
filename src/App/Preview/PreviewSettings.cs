namespace Flect.ExplorerPreview;

internal sealed class PreviewSettings
{
    public const string DefaultFont = "HackGen Console";

    public bool Enabled { get; set; } = true;
    public string FontFamily { get; set; } = DefaultFont;
    public int FontSize { get; set; } = 13;
    public string SampleText { get; set; } = string.Empty;
    public List<string> TrustedFolders { get; set; } = [];
    public List<string> DisabledKinds { get; set; } = [];
    public List<string> ThirdPartyOverrides { get; set; } = [];
}
