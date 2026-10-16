namespace Optimizer.Main.Media.Compression;

internal static class Planner
{
    public static Plan Build(MediaInfo source, long targetBytes, VideoContainer container)
    {
        long safeTarget = Bitrate.SafeTarget(targetBytes);
        var audio = BuildAudio(source, container);

        long aimBytes = (long)(safeTarget * 0.93);
        int videoKbps = Bitrate.VideoKbps(aimBytes, Math.Max(0.1, source.DurationSeconds),
                                          audio.EffectiveKbps);

        if (source.VideoBitrate > 0)
        {
            int sourceKbps = (int)(source.VideoBitrate / 1000);
            if (videoKbps > sourceKbps)
            {
                videoKbps = Math.Max(Bitrate.MinVideoKbps, (int)(sourceKbps * 0.98));
            }
        }

        if (videoKbps < Bitrate.MinVideoKbps) videoKbps = Bitrate.MinVideoKbps;

        return new Plan(
            safeTarget,
            ChooseStrategy(source, audio, container),
            Profile.Build(source, videoKbps, audio.EffectiveKbps),
            audio,
            source,
            container);
    }

    private static AudioPlan BuildAudio(MediaInfo source, VideoContainer container)
    {
        if (source.AudioTrackCount == 0) return new AudioPlan(AudioMode.None, 0, 0);

        int requested = Bitrate.DefaultAudioKbps(source.AudioChannels);
        int? sourceKbps = source.AudioBitrate > 0
            ? (int)Math.Round(source.AudioBitrate / 1000.0)
            : null;

        if (Containers.CanCopyAudio(source, container) && sourceKbps is int kbps && kbps <= requested)
        {
            return new AudioPlan(AudioMode.Copy, kbps, requested);
        }
        return new AudioPlan(AudioMode.Encode, requested, requested);
    }

    private static InitialStrategy ChooseStrategy(MediaInfo source, AudioPlan audio,
                                                  VideoContainer container)
    {
        bool videoCopyable = Containers.CanCopyVideo(source, container);
        bool hasRemovableTracks = source.AudioTrackCount > 1 || source.SubtitleTrackCount > 0;
        bool remuxable = videoCopyable
                         && (source.AudioTrackCount == 0 || Containers.CanCopyAudio(source, container))
                         && (!Containers.AlreadyIn(source, container) || hasRemovableTracks);

        if (remuxable) return InitialStrategy.Remux;

        if (videoCopyable && audio.Mode == AudioMode.Encode)
        {
            return InitialStrategy.AudioOnly;
        }

        return InitialStrategy.VideoEncode;
    }
}
