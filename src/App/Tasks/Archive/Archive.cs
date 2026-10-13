using System.Diagnostics;
using Optimizer.Main.Process;

namespace Optimizer.Tasks.Archive;

internal static class SevenZip
{
    private const ProcessPriorityClass Background = ProcessPriorityClass.BelowNormal;

    private const int WarningExitCode = 1;
    private const int ShownWarnings = 10;

    public static Task<IReadOnlyList<string>> RunAsync(IEnumerable<string> arguments,
                                                       ITaskProgress progress,
                                                       string? workingDirectory = null) =>
        RunCoreAsync(Tools.SevenZip, arguments, progress, workingDirectory);

    public static Task<IReadOnlyList<string>> RunFullAsync(IEnumerable<string> arguments,
                                                           ITaskProgress progress,
                                                           string? workingDirectory = null) =>
        RunCoreAsync(Tools.SevenZipFull, arguments, progress, workingDirectory);

    public static void Report(IEnumerable<string> warnings, string name, ITaskProgress progress)
    {
        var lines = warnings.Distinct(StringComparer.Ordinal).ToList();
        foreach (var line in lines.Take(ShownWarnings)) progress.Warn($"{name} - {line}");
        if (lines.Count > ShownWarnings)
        {
            progress.Warn($"{name} - {Loc.F("msg.andMore", Formatting.Count(lines.Count - ShownWarnings))}");
        }
    }

    private static async Task<IReadOnlyList<string>> RunCoreAsync(string executable,
                                                                  IEnumerable<string> arguments,
                                                                  ITaskProgress progress,
                                                                  string? workingDirectory)
    {
        var result = await ProcessRunner.RunAsync(executable, arguments, progress.Token,
                                                  workingDirectory: workingDirectory,
                                                  priority: Background);

        if (result.ExitCode == 0) return [];
        if (result.ExitCode == WarningExitCode) return Warnings(result);

        var detail = result.StandardError.Trim();
        if (detail.Length == 0) detail = result.StandardOutput.Trim();

        var tail = string.Join(' ', detail.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                          .TakeLast(3).Select(line => line.Trim()));
        throw new InvalidOperationException($"7-Zip failed (exit {result.ExitCode}). {tail}");
    }

    private static IReadOnlyList<string> Warnings(ProcessResult result)
    {
        var listed = ListedWarnings(result.StandardOutput);
        if (listed.Count > 0) return listed;

        var lines = Lines(result.StandardError);
        return lines.Count > 0 ? [string.Join(' ', lines)] : [Loc.T("msg.archiveWarning")];
    }

    private static List<string> ListedWarnings(string output)
    {
        var found = new List<string>();
        bool inside = false;
        foreach (var line in Lines(output))
        {
            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                inside = false;
            }
            else if (line.Contains("WARNINGS for files", StringComparison.OrdinalIgnoreCase))
            {
                inside = true;
            }
            else if (inside)
            {
                found.Add(line);
            }
        }
        return found;
    }

    private static List<string> Lines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
}

internal sealed class ArchiveExtract(Request request) : BatchTask(request)
{
    public override string Title => Loc.T("menu.archive.extract");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        progress.Indeterminate();
        progress.Status(Loc.F("label.extracting", Path.GetFileName(path)));

        var destination = OutputPath.DeriveDirectory(path, ArchiveStem(path));

        var warnings = Kind(path) switch
        {
            "rar" => await SevenZip.RunFullAsync(["x", path, "-o" + destination, "-y"], progress),
            "tar.gz" => await ExtractTarGzAsync(path, destination, progress),
            _ => await SevenZip.RunAsync(["x", path, "-o" + destination, "-y"], progress),
        };
        SevenZip.Report(warnings, Path.GetFileName(path), progress);
    }

    private static async Task<IReadOnlyList<string>> ExtractTarGzAsync(string archive,
                                                                       string destination,
                                                                       ITaskProgress progress)
    {
        using var scratch = new ScratchDirectory(destination);
        var outer = await SevenZip.RunAsync(["x", archive, "-o" + scratch.Root, "-y"], progress);

        var inner = scratch.Files().FirstOrDefault()
            ?? throw new InvalidOperationException(Loc.T("msg.archiveNoTar"));

        var contents = await SevenZip.RunAsync(["x", inner, "-o" + destination, "-y"], progress);
        return [.. outer, .. contents];
    }

    private static string Kind(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith(".tar.gz")) return "tar.gz";
        return Path.GetExtension(name).TrimStart('.');
    }

    private static string? ArchiveStem(string path)
    {
        var name = Path.GetFileName(path);
        if (name.ToLowerInvariant().EndsWith(".tar.gz")) return name[..^7];
        return Path.GetFileNameWithoutExtension(name);
    }
}