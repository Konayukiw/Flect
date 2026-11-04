namespace Flect.Image;

internal sealed class ImageSettings
{
    public ImagePresets Presets { get; set; } = new();

    public bool AllowWebpFallback { get; set; } = true;
    public int WebpQuality { get; set; } = 80;

    public BackgroundRemovalMode BackgroundMode { get; set; } = BackgroundRemovalMode.AiWithFallback;
    public string BackgroundKeyColor { get; set; } = "#00FF00";
    public double BackgroundTolerance { get; set; } = 25;

    public MetadataSettings Metadata { get; set; } = new();
}
