namespace Flect.Video;

internal sealed class VideoPresets
{
    public const long DiscordLimit = 20L * 1024 * 1024;
    public const long LegacyDiscordLimit = 10L * 1024 * 1024;

    public double ResizeA { get; set; } = 50;
    public double ResizeB { get; set; } = 25;

    public long CompressDiscord { get; set; } = DiscordLimit;
    public long CompressA { get; set; } = 50L * 1024 * 1024;
    public long CompressB { get; set; } = 25L * 1024 * 1024;
    public long CompressC { get; set; } = 5L * 1024 * 1024;

    public double RotateA { get; set; } = 45;
    public double RotateB { get; set; } = 90;
    public double RotateC { get; set; } = 180;
}
