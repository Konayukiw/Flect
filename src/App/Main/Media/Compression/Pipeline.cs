using Optimizer.Main.Process;

namespace Optimizer.Main.Media.Compression;

internal sealed record CompressionOutcome(string Path, long Bytes, bool TargetReached);

internal sealed class Pipeline(Plan plan, ITaskProgress progress)
{
    public async Task<CompressionOutcome> RunAsync(string input, ScratchDirectory scratch)
    {
        double duration = plan.Source.DurationSeconds;

        if (plan.Strategy == InitialStrategy.Remux)
        {
            var remux = scratch.PathFor("remux" + Command.Extension());
            var size = await TryStageAsync(Command.Remux(input, remux), remux,
                                           "Repackaging", duration);
            if (size is long bytes) return new CompressionOutcome(remux, bytes, true);
        }

        if (plan.Source.VideoCopyable && plan.Audio.Mode == AudioMode.Encode)
        {
            var audioOnly = scratch.PathFor("audio" + Command.Extension());
            var size = await TryStageAsync(Command.AudioOnly(input, audioOnly, plan),
                                           audioOnly, "Compressing audio", duration);
            if (size is long bytes) return new CompressionOutcome(audioOnly, bytes, true);
        }

        int videoKbps = plan.Profile.VideoKbps;
        string? best = null;
        long bestBytes = long.MaxValue;

        for (int attempt = 1; attempt <= plan.MaxFullAttempts; attempt++)
        {
            progress.Token.ThrowIfCancellationRequested();

            var encode = scratch.PathFor($"encode-{attempt}" + Command.Extension());
            var label = $"Encoding {plan.Profile.Width}x{plan.Profile.Height}" +
                        $"@{plan.Profile.Fps:0}fps · {videoKbps}kbps";

            await Ffmpeg.RunEncodeAsync(
                Command.Encoder(),
                (encoder, threads) =>
                    Command.SinglePass(input, encode, plan, encoder, videoKbps, threads),
                progress, label, duration, plan.Profile.Height);

            long size = new FileInfo(encode).Length;
            if (size <= plan.SafeTargetBytes)
            {
                return new CompressionOutcome(encode, size, true);
            }

            if (size < bestBytes)
            {
                if (best is not null) OutputPath.SafeDelete(best);
                best = encode;
                bestBytes = size;
            }
            else
            {
                OutputPath.SafeDelete(encode);
            }

            if (attempt < plan.MaxFullAttempts)
            {
                videoKbps = Math.Max(10,
                    (int)(videoKbps * ((double)plan.SafeTargetBytes / size) * 0.97));
                progress.Status($"Adjusting to {videoKbps}kbps");
            }
        }

        if (best is not null) return new CompressionOutcome(best, bestBytes, false);

        throw new InvalidOperationException("Every compression attempt failed for this file.");
    }

    public async Task<CompressionOutcome> RunAudioOnlySourceAsync(string input,
                                                                  ScratchDirectory scratch)
    {
        double duration = plan.Source.DurationSeconds;
        long budgetKbps = (long)(plan.SafeTargetBytes * 8.0 / Math.Max(0.1, duration) / 1000.0);
        int audioKbps = (int)Math.Clamp(budgetKbps - 4, 8, 320);

        var output = scratch.PathFor("audio-source.m4a");
        await Ffmpeg.RunAsync(Command.AudioOnlySource(input, output, audioKbps), progress,
                              $"Encoding audio · {audioKbps}kbps", duration);

        long size = new FileInfo(output).Length;
        return new CompressionOutcome(output, size, size <= plan.SafeTargetBytes);
    }

    private async Task<long?> TryStageAsync(string[] arguments, string output, string label,
                                            double duration)
    {
        progress.Status(label);
        try
        {
            await Ffmpeg.RunAsync(arguments, progress, label, duration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            OutputPath.SafeDelete(output);
            return null;
        }

        if (!File.Exists(output)) return null;

        long size = new FileInfo(output).Length;
        if (size <= plan.SafeTargetBytes) return size;

        OutputPath.SafeDelete(output);
        return null;
    }
}
