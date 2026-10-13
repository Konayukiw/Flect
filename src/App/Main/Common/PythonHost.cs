namespace Optimizer.Main.Common;

internal sealed record PythonRunResult(int ExitCode, string Output, string Error);

internal static class PythonHost
{
    private static readonly Dictionary<string, string> Utf8Output = new()
    {
        ["PYTHONUTF8"] = "1",
        ["PYTHONIOENCODING"] = "utf-8",
    };

    public static string? FindSystemPython()
    {
        var candidates = new List<string>();

        var pyLauncher = FindOnPath("py.exe");
        if (pyLauncher is not null)
        {
            candidates.Add(pyLauncher + " -3");
        }

        var python = FindOnPath("python.exe");
        if (python is not null) candidates.Add(python);

        var python3 = FindOnPath("python3.exe");
        if (python3 is not null) candidates.Add(python3);

        var localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
        if (Directory.Exists(localPrograms))
        {
            try
            {
                var exes = Directory.EnumerateFiles(localPrograms, "python.exe", SearchOption.AllDirectories).Take(3);
                candidates.AddRange(exes);
            }
            catch { }
        }

        foreach (var cand in candidates)
        {
            try
            {
                string file, args;
                if (cand.EndsWith(" -3", StringComparison.Ordinal))
                {
                    file = cand[..^3].Trim();
                    args = "-3 --version";
                }
                else
                {
                    file = cand;
                    args = "--version";
                }
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p is null) continue;
                p.WaitForExit(5000);
                if (p.ExitCode == 0) return cand;
            }
            catch { continue; }
        }
        return null;
    }

    public static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    public static async Task<PythonRunResult> RunProcessAsync(string python, IEnumerable<string> arguments,
                                                              CancellationToken token,
                                                              Action<string>? onOutputLine = null,
                                                              IReadOnlyDictionary<string, string>? environment = null)
    {
        var (file, launcherArguments) = Split(python);

        var variables = new Dictionary<string, string>(Utf8Output);
        if (environment is not null)
        {
            foreach (var (name, value) in environment) variables[name] = value;
        }

        var result = await ProcessRunner.RunAsync(file, [.. launcherArguments, .. arguments], token,
                                                  workingDirectory: AppContext.BaseDirectory,
                                                  onOutputLine: onOutputLine,
                                                  environment: variables).ConfigureAwait(false);
        return new PythonRunResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);

    private static (string File, string[] Arguments) Split(string python) =>
        python.EndsWith(" -3", StringComparison.Ordinal)
            ? (python[..^3].Trim(), ["-3"])
            : (python, []);
}
