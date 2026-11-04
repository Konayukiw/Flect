namespace Flect.Video.Thumbnail;

internal sealed class ThumbnailSettings
{
    public ThumbnailFormat Format { get; set; } = ThumbnailFormat.Png;
    public int Quality { get; set; } = 90;
    public int MaxWidth { get; set; }
}
