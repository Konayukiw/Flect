namespace Flect.Video.Compress;

internal sealed record Plan(
    long SafeTargetBytes,
    InitialStrategy Strategy,
    EncodingProfile Profile,
    AudioPlan Audio,
    MediaInfo Source,
    VideoContainer Container)
{
    public int MaxFullAttempts => 2;

    public bool VideoCopyable => global::Flect.Video.Container.CanCopyVideo(Source, Container);

    public string Extension => global::Flect.Video.Container.Extension(Container);
}
