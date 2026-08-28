namespace Optimizer.Main.Common;

using System.Text;

internal sealed record PythonRunResult(int ExitCode, string Output, string Error);

internal static class PythonHost
{
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

    public static Task<PythonRunResult> RunProcessAsync(string fileName, string arguments, CancellationToken token)
    {
        var file = fileName;
        var args = arguments;
        if (fileName.EndsWith(" -3", StringComparison.Ordinal))
        {
            file = fileName[..^3].Trim();
            args = "-3 " + arguments;
        }

        return Task.Run(() =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            var output = new StringBuilder();
            var error = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            while (!proc.WaitForExit(200))
            {
                token.ThrowIfCancellationRequested();
            }
            proc.WaitForExit();
            return new PythonRunResult(proc.ExitCode, output.ToString(), error.ToString());
        }, token);
    }
}
