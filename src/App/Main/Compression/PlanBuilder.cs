namespace Optimizer.Main.Compression;

internal static class PlanBuilder
{
    public static CompressionPlan Build(MediaInfo source, long targetBytes)
    {
        long safeTarget = Bitrate.SafeTarget(targetBytes);
        var audio = BuildAudio(source);

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

        return new CompressionPlan(
            safeTarget,
            ChooseStrategy(source, audio),
            ProfileBuilder.Build(source, videoKbps, audio.EffectiveKbps),
            audio,
            source);
    }

    private static AudioPlan BuildAudio(MediaInfo source)
    {
        if (source.AudioTrackCount == 0) return new AudioPlan(AudioMode.None, 0, 0);

        int requested = Bitrate.DefaultAudioKbps(source.AudioChannels);
        int? sourceKbps = source.AudioBitrate > 0
            ? (int)Math.Round(source.AudioBitrate / 1000.0)
            : null;

        if (source.AudioCopyable && sourceKbps is int kbps && kbps <= requested)
        {
            return new AudioPlan(AudioMode.Copy, kbps, requested);
        }
        return new AudioPlan(AudioMode.Encode, requested, requested);
    }

    private static InitialStrategy ChooseStrategy(MediaInfo source, AudioPlan audio)
    {
        bool hasRemovableTracks = source.AudioTrackCount > 1 || source.SubtitleTrackCount > 0;
        bool remuxable = source.VideoCopyable
                         && (source.AudioTrackCount == 0 || source.AudioCopyable)
                         && (!source.IsMp4Family || hasRemovableTracks);

        if (remuxable) return InitialStrategy.Remux;

        if (source.VideoCopyable && audio.Mode == AudioMode.Encode)
        {
            return InitialStrategy.AudioOnly;
        }

        return InitialStrategy.VideoEncode;
    }
}
