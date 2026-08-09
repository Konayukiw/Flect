using System.Diagnostics;

namespace Optimizer.Main;

internal static class Ffmpeg
{
    public static readonly string[] Preamble =
        ["-hide_banner", "-loglevel", "error", "-nostdin", "-progress", "pipe:1", "-y"];

    public const string EvenDimensions = "scale=trunc(iw/2)*2:trunc(ih/2)*2";

    private const ProcessPriorityClass Background = ProcessPriorityClass.BelowNormal;

    public static Task RunAsync(IEnumerable<string> arguments, ITaskProgress progress, string label,
                                double durationSeconds) =>
        ProcessRunner.RunOrThrowAsync(Tools.Ffmpeg, arguments, progress.Token,
            onOutputLine: line => ReportProgress(line, progress, label, durationSeconds),
            priority: Background);

    public static async Task RunEncodeAsync(Func<int, IEnumerable<string>> build,
                                            ITaskProgress progress, string label,
                                            double durationSeconds, int outputHeight)
    {
        var rungs = EncodePolicy.Ladder(outputHeight);

        for (int attempt = 0; attempt < rungs.Count; attempt++)
        {
            int threads = rungs[attempt];
            try
            {
                await RunAsync(build(threads), progress, label, durationSeconds);
                EncodePolicy.RecordSuccess(threads);
                return;
            }
            catch (ProcessFailure failure)
                when (failure.Crashed && attempt < rungs.Count - 1)
            {
                progress.Status($"{label} — retrying on {rungs[attempt + 1]} threads");
            }
        }
    }

    private static void ReportProgress(string line, ITaskProgress progress, string label,
                                       double durationSeconds)
    {
        const string key = "out_time_us=";
        if (durationSeconds <= 0 || !line.StartsWith(key, StringComparison.Ordinal)) return;
        if (!long.TryParse(line.AsSpan(key.Length), out var microseconds) || microseconds < 0) return;

        double done = Math.Clamp(microseconds / 1_000_000.0 / durationSeconds, 0, 1);
        progress.Status($"{label} — {done:P0}");
    }
}
