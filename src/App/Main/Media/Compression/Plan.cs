namespace Optimizer.Main.Media.Compression;

internal enum AudioMode { None, Copy, Encode }

internal enum InitialStrategy { Remux, AudioOnly, VideoEncode }

internal sealed record AudioPlan(AudioMode Mode, int EffectiveKbps, int EncodeKbps);

internal sealed record EncodingProfile(int Width, int Height, double Fps, int VideoKbps,
                                       int AudioKbps);

internal sealed record Plan(
    long SafeTargetBytes,
    InitialStrategy Strategy,
    EncodingProfile Profile,
    AudioPlan Audio,
    MediaInfo Source)
{
    public int MaxFullAttempts => 2;
}
