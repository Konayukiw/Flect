using System.Diagnostics;
using System.Text;

namespace Optimizer.Main;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

internal sealed class ProcessFailure(string message, int exitCode) : InvalidOperationException(message)
{
    public int ExitCode { get; } = exitCode;

    public bool Crashed => ExitCode < 0;
}

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
                                                     CancellationToken token,
                                                     Encoding? outputEncoding = null,
                                                     string? workingDirectory = null,
                                                     Action<string>? onOutputLine = null,
                                                     ProcessPriorityClass? priority = null)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = outputEncoding ?? Encoding.UTF8,
            StandardErrorEncoding = outputEncoding ?? Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) ?? string.Empty,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = info };
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            standardOutput.AppendLine(e.Data);
            onOutputLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) standardError.AppendLine(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {Path.GetFileName(executable)}.");
        }

        if (priority is not null)
        {
            try { process.PriorityClass = priority.Value; }
            catch (Exception) { 
            }
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    public static async Task<ProcessResult> RunOrThrowAsync(string executable,
                                                            IEnumerable<string> arguments,
                                                            CancellationToken token,
                                                            Encoding? outputEncoding = null,
                                                            Action<string>? onOutputLine = null,
                                                            ProcessPriorityClass? priority = null)
    {
        var result = await RunAsync(executable, arguments, token, outputEncoding,
                                    onOutputLine: onOutputLine, priority: priority);
        if (result.Succeeded) return result;

        var detail = result.StandardError.Trim();
        if (detail.Length == 0) detail = result.StandardOutput.Trim();

        var lines = detail.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var tail = string.Join(' ', lines.TakeLast(3).Select(line => line.Trim()));

        throw new ProcessFailure(
            $"{Path.GetFileNameWithoutExtension(executable)} failed (exit {result.ExitCode}). {tail}",
            result.ExitCode);
    }
}
