namespace Flect.Video.Compress;

internal sealed record CompressionOutcome(string Path, long Bytes, bool TargetReached);
