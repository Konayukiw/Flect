namespace Evals.Infrastructure;

internal static class Tool
{
    public static Task FfmpegAsync(params string[] arguments) =>
        ProcessRunner.RunOrThrowAsync(Tools.Ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", .. arguments], CancellationToken.None);

    public static async Task<Dictionary<string, string>> ProbeAsync(string file, string selector, string entries)
    {
        var result = await ProcessRunner.RunOrThrowAsync(Tools.Ffprobe,
            ["-v", "error", "-select_streams", selector, "-show_entries", entries, "-of", "default=noprint_wrappers=1", file],
            CancellationToken.None);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = line.Trim().Split('=', 2);
            if (pair.Length == 2) values.TryAdd(pair[0].Replace("side_data/", string.Empty), pair[1]);
        }
        return values;
    }

    public static async Task<string> VideoCodecAsync(string file) =>
        (await ProbeAsync(file, "v:0", "stream=codec_name")).GetValueOrDefault("codec_name", string.Empty);

    public static async Task<string> AudioCodecAsync(string file) =>
        (await ProbeAsync(file, "a:0", "stream=codec_name")).GetValueOrDefault("codec_name", string.Empty);

    public static async Task<(int Width, int Height)> SizeAsync(string file)
    {
        var values = await ProbeAsync(file, "v:0", "stream=width,height");
        return (int.Parse(values["width"]), int.Parse(values["height"]));
    }

    public static async Task<double> DurationAsync(string file)
    {
        var result = await ProcessRunner.RunOrThrowAsync(Tools.Ffprobe,
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", file],
            CancellationToken.None);
        return double.Parse(result.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string? FindOnPath(string fileName) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim('"'), fileName))
            .FirstOrDefault(File.Exists);

    public static bool Available(Func<string> resolve)
    {
        try
        {
            resolve();
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
}
