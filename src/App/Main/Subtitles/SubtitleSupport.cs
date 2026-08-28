using System.Text.Json;

namespace Optimizer.Main.Subtitles;

using Optimizer.Main.Common;
using Optimizer.Main.Config;
using Optimizer.Main.Localization;
using Optimizer.Main.Process;

internal static class SubtitleSupport
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
            var bundled = Path.Combine(AppContext.BaseDirectory, "deps", "subtitles.py");
            if (File.Exists(bundled)) return bundled;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "deps", "subtitles.py");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            var cur = new DirectoryInfo(Environment.CurrentDirectory);
            for (int i = 0; i < 6 && cur is not null; i++)
            {
                var candidate = Path.Combine(cur.FullName, "deps", "subtitles.py");
                if (File.Exists(candidate)) return candidate;
                cur = cur.Parent;
            }

            return bundled;
        }
    }

    public static async Task EnsureEnvironmentAsync(ITaskProgress progress, CancellationToken token,
                                                    bool preferCuda)
    {
        Directory.CreateDirectory(ModelsDir);

        var cudaNeeded = preferCuda && PythonHost.FindOnPath("nvidia-smi.exe") is not null;

        if (File.Exists(EnvPython))
        {
            if (await CanImportAsync(token).ConfigureAwait(false))
            {
                if (cudaNeeded) await EnsureCudaAsync(progress, token).ConfigureAwait(false);
                return;
            }
            progress.Status(Loc.T("msg.subtitlesUpdating"));
        }
        else
        {
            progress.Status(Loc.T("msg.subtitlesSettingUp"));
        }

        var systemPython = PythonHost.FindSystemPython();
        if (systemPython is null)
            throw new FileNotFoundException(Loc.T("msg.subtitlesNoPython"));

        if (!File.Exists(EnvPython))
        {
            progress.Info(Loc.T("msg.subtitlesCreatingEnv"));
            var venvResult = await PythonHost.RunProcessAsync(systemPython, $"-m venv \"{EnvRoot}\"", token).ConfigureAwait(false);
            if (venvResult.ExitCode != 0)
                throw new InvalidOperationException($"{Loc.T("msg.bgVenvFailed")}: {venvResult.Error}");
            if (!File.Exists(EnvPython))
                throw new InvalidOperationException(Loc.T("msg.bgVenvFailed"));
        }

        progress.Info(Loc.T("msg.bgUpgradingPip"));
        var pipUpgrade = await PythonHost.RunProcessAsync(EnvPython, "-m pip install --upgrade pip --quiet --disable-pip-version-check", token).ConfigureAwait(false);

        progress.Info(Loc.T("msg.subtitlesInstallingDeps"));
        var install = await PythonHost.RunProcessAsync(EnvPython,
            "-m pip install --quiet --disable-pip-version-check faster-whisper", token).ConfigureAwait(false);
        if (install.ExitCode != 0)
            throw new InvalidOperationException($"{Loc.T("msg.subtitlesInstallFailed")}: {install.Error}");

        if (cudaNeeded) await EnsureCudaAsync(progress, token).ConfigureAwait(false);

        if (!await CanImportAsync(token).ConfigureAwait(false))
            throw new InvalidOperationException(Loc.T("msg.subtitlesInstallFailed"));

        progress.Info(Loc.T("msg.subtitlesReady"));
    }

    private static async Task EnsureCudaAsync(ITaskProgress progress, CancellationToken token)
    {
        if (CudaLibsPresent() && await CudaCountAsync(token) > 0) return;

        progress.Info(Loc.T("msg.subtitlesInstallingCuda"));
        var install = await PythonHost.RunProcessAsync(EnvPython,
            "-m pip install --quiet --disable-pip-version-check nvidia-cublas-cu12 nvidia-cudnn-cu12", token).ConfigureAwait(false);
        if (install.ExitCode != 0)
        {
            progress.Warn(Loc.T("msg.subtitlesCudaFailed"));
            return;
        }

        if (await CudaCountAsync(token) > 0)
        {
            progress.Info(Loc.T("msg.subtitlesCudaReady"));
            return;
        }

        progress.Warn(Loc.T("msg.subtitlesCudaFailed"));
    }

    private static bool CudaLibsPresent()
    {
        var site = Path.Combine(EnvRoot, "Lib", "site-packages");
        return File.Exists(Path.Combine(site, "nvidia", "cublas", "bin", "cublas64_12.dll")) &&
               File.Exists(Path.Combine(site, "nvidia", "cudnn", "bin", "cudnn64_9.dll"));
    }

    private static async Task<int> CudaCountAsync(CancellationToken token)
    {
        var probe = await PythonHost.RunProcessAsync(EnvPython,
            "-c \"import ctranslate2; print(ctranslate2.get_cuda_device_count())\"", token).ConfigureAwait(false);
        if (probe.ExitCode != 0) return 0;
        return int.TryParse(probe.Output.Trim(), out var count) ? count : 0;
    }

    private static async Task<bool> CanImportAsync(CancellationToken token)
    {
        var r = await PythonHost.RunProcessAsync(EnvPython,
            "-c \"import faster_whisper; print('ok')\"", token).ConfigureAwait(false);
        return r.ExitCode == 0 && r.Output.Contains("ok");
    }

    public static async Task<IReadOnlyList<FileResult>> TranscribeBatchAsync(
        IReadOnlyList<(string Input, string Output, string Wav)> items,
        SubtitlesSettings settings,
        ITaskProgress progress,
        CancellationToken token)
    {
        if (items.Count == 0) return [];

        await EnsureEnvironmentAsync(progress, token, settings.Device == SubtitleDevice.Auto).ConfigureAwait(false);

        var manifest = Path.Combine(Path.GetTempPath(), $"{Branding.Name}-subtitles-{Guid.NewGuid():N}.json");
        try
        {
            var payload = items.Select(p => new { input = p.Wav, output = p.Output }).ToList();
            var json = JsonSerializer.Serialize(payload);
            await File.WriteAllTextAsync(manifest, json, token).ConfigureAwait(false);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = EnvPython,
                Arguments = $"\"{ToolScript}\" --model {ModelName(settings.Model)} " +
                            $"--device {DeviceName(settings.Device)} " +
                            $"--language {LanguageArg(settings.Language)} " +
                            $"--format {FormatName(settings.Output)} " +
                            $"--manifest \"{manifest}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.Environment["HF_HOME"] = ModelsDir;
            psi.Environment["PYTHONUTF8"] = "1";
            psi.Environment["PYTHONIOENCODING"] = "utf-8";

            progress.Status(Loc.T("msg.subtitlesTranscribing"));

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
                if (!string.IsNullOrWhiteSpace(line)) progress.Info(line.Trim());
            }

            if (proc.ExitCode != 0)
            {
                var detail = string.Join(Environment.NewLine, stderrLines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(5));
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"subtitles process exited with code {proc.ExitCode}"
                        : detail);
            }

            var perFile = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var emptyFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            lock (stdoutLines)
            {
                foreach (var line in stdoutLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("notice", out var notice))
                        {
                            progress.Info(notice.GetString() ?? string.Empty);
                            continue;
                        }
                        if (root.TryGetProperty("progress", out var fraction) &&
                            root.TryGetProperty("input", out var input))
                        {
                            var name = Path.GetFileName(input.GetString() ?? string.Empty);
                            var percent = fraction.GetDouble();
                            progress.Status($"{name} — {percent:P0}");
                            continue;
                        }

                        var inputPath = root.GetProperty("input").GetString() ?? string.Empty;
                        var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                        perFile[inputPath] = ok;
                        if (ok && root.TryGetProperty("empty", out var empty) && empty.GetBoolean())
                            emptyFiles.Add(inputPath);
                        if (!ok && root.TryGetProperty("error", out var err))
                            errors[inputPath] = err.GetString() ?? "unknown error";
                    }
                    catch { /* ignore */ }
                }
            }

            var results = items.Select(p =>
            {
                var ok = perFile.TryGetValue(p.Wav, out var v) ? v : File.Exists(p.Output);
                errors.TryGetValue(p.Wav, out var err);
                return new FileResult(p.Input, p.Output, ok, err, emptyFiles.Contains(p.Wav));
            }).ToList();

            return results;
        }
        finally
        {
            try { if (File.Exists(manifest)) File.Delete(manifest); } catch { }
        }
    }

    private static string ModelName(SubtitleModel model) => model switch
    {
        SubtitleModel.Tiny => "tiny",
        SubtitleModel.Base => "base",
        SubtitleModel.Medium => "medium",
        SubtitleModel.LargeV3 => "large-v3",
        SubtitleModel.Turbo => "turbo",
        _ => "small",
    };

    private static string DeviceName(SubtitleDevice device) =>
        device == SubtitleDevice.Cpu ? "cpu" : "auto";

    private static string LanguageArg(string language) =>
        string.IsNullOrWhiteSpace(language) ? "auto" : language;

    private static string FormatName(SubtitleOutput output) => output switch
    {
        SubtitleOutput.Vtt => "vtt",
        SubtitleOutput.Txt => "txt",
        _ => "srt",
    };

    private static Task WaitForExitAsync(System.Diagnostics.Process process, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => tcs.TrySetResult(true);
        if (process.HasExited) tcs.TrySetResult(true);
        token.Register(() => tcs.TrySetCanceled(token));
        return tcs.Task;
    }

    public sealed record FileResult(string Input, string Output, bool Ok, string? Error, bool Empty);
}
