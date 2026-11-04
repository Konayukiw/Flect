namespace Flect.Image;

internal sealed class ImagePresets
{
    public double ResizeA { get; set; } = 50;
    public double ResizeB { get; set; } = 25;

    public long CompressA { get; set; } = 10L * 1024 * 1024;
    public long CompressB { get; set; } = 5L * 1024 * 1024;
    public long CompressC { get; set; } = 3L * 1024 * 1024;
    public long CompressD { get; set; } = 1L * 1024 * 1024;

    public double RotateA { get; set; } = 45;
    public double RotateB { get; set; } = 90;
    public double RotateC { get; set; } = 180;
}
