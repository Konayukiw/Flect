namespace Flect.Image;

internal sealed class MetadataSettings
{
    public bool RemoveExif { get; set; } = true;
    public bool RemoveGps { get; set; } = true;
    public bool RemoveComments { get; set; } = true;
    public bool RemoveColorProfile { get; set; }
    public bool ApplyOrientation { get; set; } = true;
}
