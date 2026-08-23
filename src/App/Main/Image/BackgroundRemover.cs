using System.Diagnostics;
using System.Text.Json;

namespace Optimizer.Main.Image;

using Optimizer.Main.Config;
using Optimizer.Main.Localization;

internal static class BackgroundRemover
{
    private static readonly string EnvRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Branding.Name, "env");

    private static readonly string EnvPython = Path.Combine(EnvRoot, "Scripts", "python.exe");
    private static readonly string ModelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Branding.Name, "models");

    private static string ToolScript
    {
        get
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "deps", "remove_bg.py");
            if (File.Exists(bundled)) return bundled;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "deps", "remove_bg.py");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            var cur = new DirectoryInfo(Environment.CurrentDirectory);
            for (int i = 0; i < 6 && cur is not null; i++)
            {
                var candidate = Path.Combine(cur.FullName, "deps", "remove_bg.py");
                if (File.Exists(candidate)) return candidate;
                cur = cur.Parent;
            }

            return bundled;
        }
    }

    public static async Task<BatchResult> RemoveBatchAsync(
        IReadOnlyList<(string Input, string Output)> items,
        ITaskProgress progress,
        CancellationToken token)
    {
        if (items.Count == 0) return new BatchResult([]);

        await EnsureEnvironmentAsync(progress, token).ConfigureAwait(false);

        var manifest = Path.Combine(Path.GetTempPath(), $"{Branding.Name}-bg-{Guid.NewGuid():N}.json");
        try
        {
            var payload = items.Select(p => new { input = p.Input, output = p.Output }).ToList();
            var json = JsonSerializer.Serialize(payload);
            await File.WriteAllTextAsync(manifest, json, token).ConfigureAwait(false);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = EnvPython,
                Arguments = $"\"{ToolScript}\" --manifest \"{manifest}\" --model u2net",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.Environment["U2NET_HOME"] = ModelsDir;
            psi.Environment["PYTHONUTF8"] = "1";
            psi.Environment["PYTHONIOENCODING"] = "utf-8";

            Directory.CreateDirectory(ModelsDir);

            progress.Status(Loc.T("msg.removingBackground"));

            using var proc = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdoutLines = new List<string>();
            var stderrLines = new List<string>();

            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (stdoutLines) stdoutLines.Add(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (stderrLines) stderrLines.Add(e.Data); };

            try
            {
                if (!proc.Start())
                    throw new InvalidOperationException("Failed to start python process.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"{Loc.T("msg.bgPythonFailed")}: {ex.Message}", ex);
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await WaitForExitAsync(proc, token).ConfigureAwait(false);

            await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                throw new OperationCanceledException(token);
            }

            foreach (var line in stderrLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    progress.Info(line.Trim());
            }

            if (proc.ExitCode != 0)
            {
                var detail = string.Join(Environment.NewLine, stderrLines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(5));
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"rembg process exited with code {proc.ExitCode}"
                        : detail);
            }

            var perFile = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            lock (stdoutLines)
            {
                foreach (var line in stdoutLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var input = root.GetProperty("input").GetString() ?? string.Empty;
                        var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                        perFile[input] = ok;
                        if (!ok && root.TryGetProperty("error", out var err))
                            errors[input] = err.GetString() ?? "unknown error";
                    }
                    catch {/* ignore */}
                }
            }

            var results = items.Select(p =>
            {
                var ok = perFile.TryGetValue(p.Input, out var v) ? v : File.Exists(p.Output);
                errors.TryGetValue(p.Input, out var err);
                return new FileResult(p.Input, p.Output, ok, err);
            }).ToList();

            return new BatchResult(results);
        }
        finally
        {
            try { if (File.Exists(manifest)) File.Delete(manifest); } catch { }
        }
    }

    public static async Task EnsureEnvironmentAsync(ITaskProgress progress, CancellationToken token)
    {
        Directory.CreateDirectory(ModelsDir);

        if (File.Exists(EnvPython))
        {
            if (await CanImportRembgAsync(token).ConfigureAwait(false))
                return;
            progress.Status(Loc.T("msg.bgInstalling"));
        }
        else
        {
            progress.Status(Loc.T("msg.bgSettingUp"));
        }

        var systemPython = FindSystemPython();
        if (systemPython is null)
            throw new FileNotFoundException(Loc.T("msg.bgNoPython"));

        if (!File.Exists(EnvPython))
        {
            progress.Info(Loc.T("msg.bgCreatingEnv"));
            var venvResult = await RunProcessAsync(systemPython, $"-m venv \"{EnvRoot}\"", token).ConfigureAwait(false);
            if (venvResult.ExitCode != 0)
                throw new InvalidOperationException($"{Loc.T("msg.bgVenvFailed")}: {venvResult.Error}");
            if (!File.Exists(EnvPython))
                throw new InvalidOperationException(Loc.T("msg.bgVenvFailed"));
        }

        progress.Info(Loc.T("msg.bgUpgradingPip"));
        var pipUpgrade = await RunProcessAsync(EnvPython, "-m pip install --upgrade pip --quiet --disable-pip-version-check", token).ConfigureAwait(false);

        progress.Info(Loc.T("msg.bgInstallingDeps"));
        var installArgs = "-m pip install --quiet --disable-pip-version-check rembg pillow onnxruntime onnxruntime-directml";
        var install = await RunProcessAsync(EnvPython, installArgs, token).ConfigureAwait(false);
        if (install.ExitCode != 0)
        {
            var fallbackArgs = "-m pip install --quiet --disable-pip-version-check rembg pillow onnxruntime";
            var fallback = await RunProcessAsync(EnvPython, fallbackArgs, token).ConfigureAwait(false);
            if (fallback.ExitCode != 0)
                throw new InvalidOperationException($"{Loc.T("msg.bgInstallFailed")}: {fallback.Error}");
        }

        if (!await CanImportRembgAsync(token).ConfigureAwait(false))
            throw new InvalidOperationException(Loc.T("msg.bgInstallFailed"));

        progress.Info(Loc.T("msg.bgReady"));
    }

    private static async Task<bool> CanImportRembgAsync(CancellationToken token)
    {
        var r = await RunProcessAsync(EnvPython, "-c \"import rembg, PIL, onnxruntime; print('ok')\"", token).ConfigureAwait(false);
        return r.ExitCode == 0 && r.Output.Contains("ok");
    }

    private static string? FindSystemPython()
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

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);

    private static Task<RunResult> RunProcessAsync(string fileName, string arguments, CancellationToken token)
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
            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();
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
            return new RunResult(proc.ExitCode, output.ToString(), error.ToString());
        }, token);
    }

    private static Task WaitForExitAsync(System.Diagnostics.Process process, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => tcs.TrySetResult(true);
        if (process.HasExited) tcs.TrySetResult(true);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }

    public static void ApplyChromaKey(string input, string output, string keyColor, double tolerance)
    {
        using var frames = new ImageMagick.MagickImageCollection(input);
        ImageMagick.IMagickImage<byte> frame;
        if (frames.Count > 1 && frames[0].Format == ImageMagick.MagickFormat.Gif)
        {
            frames.Coalesce();
            frame = frames.OrderByDescending(f => (long)f.Width * f.Height).First();
        }
        else
        {
            frame = frames.OrderByDescending(f => (long)f.Width * f.Height).First();
        }

        var color = ColorText.Parse(keyColor, System.Windows.Media.Colors.Lime);
        frame.ColorFuzz = new ImageMagick.Percentage(Math.Clamp(tolerance, 0, 100));
        frame.Transparent(new ImageMagick.MagickColor(color.R, color.G, color.B));
        frame.ColorFuzz = new ImageMagick.Percentage(0);

        var dir = Path.GetDirectoryName(output) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(dir);
        frame.Write(output, ImageMagick.MagickFormat.Png32);
    }

    public sealed record FileResult(string Input, string Output, bool Ok, string? Error);
    public sealed record BatchResult(IReadOnlyList<FileResult> Files);
}
