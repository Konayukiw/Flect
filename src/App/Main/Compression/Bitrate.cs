namespace Optimizer.Main.Compression;

internal static class Bitrate
{
    public const int MinVideoKbps = 80;

    public static long SafeTarget(long targetBytes)
    {
        long reserve = Math.Max(64 * 1024, (long)Math.Ceiling(targetBytes * 0.003));
        return Math.Max(1, targetBytes - reserve);
    }

    public static int VideoKbps(long safeTargetBytes, double durationSeconds, int audioKbps)
    {
        double totalKbps = safeTargetBytes * 8.0 / durationSeconds / 1000.0;
        double muxingReserve = Math.Max(8, totalKbps * 0.01);
        return (int)Math.Floor(totalKbps - audioKbps - muxingReserve);
    }

    public static double BitsPerPixelFrame(int videoKbps, int width, int height, double fps) =>
        videoKbps * 1000.0 / (width * (double)height * fps);

    public static long EstimateAudioBytes(int audioKbps, double durationSeconds) =>
        (long)Math.Ceiling(audioKbps * 1000.0 * durationSeconds / 8.0);

    public static int DefaultAudioKbps(int channels) =>
        channels <= 1 ? 64 : channels <= 2 ? 128 : 160;
}
