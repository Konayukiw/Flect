using System.Text.Json;

namespace Optimizer.Main.Image;

using Optimizer.Main.Common;
using Optimizer.Main.Config;
using Optimizer.Main.Localization;

internal static class BackgroundRemover
{
    private static readonly string EnvRoot = Path.Combine(Locations.Local, "env");
    private static readonly string EnvPython = Path.Combine(EnvRoot, "Scripts", "python.exe");
    private static readonly string ModelsDir = Path.Combine(Locations.Local, "models");

    private static string ToolScript
    {
        get
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "deps", "rembg.py");
            if (File.Exists(bundled)) return bundled;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "deps", "rembg.py");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            var cur = new DirectoryInfo(Environment.CurrentDirectory);
            for (int i = 0; i < 6 && cur is not null; i++)
            {
                var candidate = Path.Combine(cur.FullName, "deps", "rembg.py");
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

            Directory.CreateDirectory(ModelsDir);

            progress.Status(Loc.T("msg.removingBackground"));
            progress.Step(0, items.Count);

            var perFile = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void OnLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var input = root.GetProperty("input").GetString() ?? string.Empty;
                    var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                    var error = !ok && root.TryGetProperty("error", out var err)
                        ? err.GetString() ?? "unknown error"
                        : null;

                    int done;
                    lock (perFile)
                    {
                        perFile[input] = ok;
                        if (error is not null) errors[input] = error;
                        done = perFile.Count;
                    }
                    progress.Step(done, items.Count);
                }
                catch (Exception)
                {
                }
            }

            PythonRunResult result;
            try
            {
                result = await PythonHost.RunProcessAsync(EnvPython,
                    [ToolScript, "--manifest", manifest, "--model", "u2net"], token, OnLine,
                    new Dictionary<string, string> { ["U2NET_HOME"] = ModelsDir }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (perFile)
                {
                    foreach (var item in items)
                    {
                        if (!perFile.GetValueOrDefault(item.Input)) OutputPath.SafeDelete(item.Output);
                    }
                }
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{Loc.T("msg.bgPythonFailed")}: {ex.Message}", ex);
            }

            foreach (var line in PythonHost.Lines(result.Error)) progress.Info(line);

            if (result.ExitCode != 0)
            {
                var detail = string.Join(Environment.NewLine, PythonHost.Lines(result.Error).Take(5));
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"rembg process exited with code {result.ExitCode}"
                        : detail);
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

        var systemPython = PythonHost.FindSystemPython();
        if (systemPython is null)
            throw new FileNotFoundException(Loc.T("msg.bgNoPython"));

        if (!File.Exists(EnvPython))
        {
            progress.Info(Loc.T("msg.bgCreatingEnv"));
            var venvResult = await PythonHost.RunProcessAsync(systemPython, ["-m", "venv", EnvRoot], token).ConfigureAwait(false);
            if (venvResult.ExitCode != 0)
                throw new InvalidOperationException($"{Loc.T("msg.bgVenvFailed")}: {venvResult.Error}");
            if (!File.Exists(EnvPython))
                throw new InvalidOperationException(Loc.T("msg.bgVenvFailed"));
        }

        progress.Info(Loc.T("msg.bgUpgradingPip"));
        var pipUpgrade = await PythonHost.RunProcessAsync(EnvPython, ["-m", "pip", "install", "--upgrade", "pip", "--quiet", "--disable-pip-version-check"], token).ConfigureAwait(false);

        progress.Info(Loc.T("msg.bgInstallingDeps"));
        string[] installArgs = ["-m", "pip", "install", "--quiet", "--disable-pip-version-check", "rembg", "pillow", "onnxruntime", "onnxruntime-directml"];
        var install = await PythonHost.RunProcessAsync(EnvPython, installArgs, token).ConfigureAwait(false);
        if (install.ExitCode != 0)
        {
            string[] fallbackArgs = ["-m", "pip", "install", "--quiet", "--disable-pip-version-check", "rembg", "pillow", "onnxruntime"];
            var fallback = await PythonHost.RunProcessAsync(EnvPython, fallbackArgs, token).ConfigureAwait(false);
            if (fallback.ExitCode != 0)
                throw new InvalidOperationException($"{Loc.T("msg.bgInstallFailed")}: {fallback.Error}");
        }

        if (!await CanImportRembgAsync(token).ConfigureAwait(false))
            throw new InvalidOperationException(Loc.T("msg.bgInstallFailed"));

        progress.Info(Loc.T("msg.bgReady"));
    }

    private static async Task<bool> CanImportRembgAsync(CancellationToken token)
    {
        var r = await PythonHost.RunProcessAsync(EnvPython, ["-c", "import rembg, PIL, onnxruntime; print('ok')"], token).ConfigureAwait(false);
        return r.ExitCode == 0 && r.Output.Contains("ok");
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
