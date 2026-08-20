using System.Diagnostics;
using Optimizer.Main.Process;

namespace Optimizer.Tasks.Archive;

internal static class SevenZip
{
    private const ProcessPriorityClass Background = ProcessPriorityClass.BelowNormal;

    public static Task RunAsync(IEnumerable<string> arguments, ITaskProgress progress,
                                string? workingDirectory = null) =>
        RunCoreAsync(Tools.SevenZip, arguments, progress, workingDirectory);

    public static Task RunFullAsync(IEnumerable<string> arguments, ITaskProgress progress,
                                    string? workingDirectory = null) =>
        RunCoreAsync(Tools.SevenZipFull, arguments, progress, workingDirectory);

    private static async Task RunCoreAsync(string executable, IEnumerable<string> arguments,
                                           ITaskProgress progress, string? workingDirectory)
    {
        var result = await ProcessRunner.RunAsync(executable, arguments, progress.Token,
                                                  workingDirectory: workingDirectory,
                                                  priority: Background);

        if (result.ExitCode <= 1) return;

        var detail = result.StandardError.Trim();
        if (detail.Length == 0) detail = result.StandardOutput.Trim();

        var tail = string.Join(' ', detail.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                          .TakeLast(3).Select(line => line.Trim()));
        throw new InvalidOperationException($"7-Zip failed (exit {result.ExitCode}). {tail}");
    }
}

internal sealed class ArchiveExtract(Request request) : BatchTask(request)
{
    public override string Title => Loc.T("menu.archive.extract");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        progress.Indeterminate();
        progress.Status(Loc.F("label.extracting", Path.GetFileName(path)));

        var destination = OutputPath.DeriveDirectory(path, ArchiveStem(path));

        switch (Kind(path))
        {
            case "rar":
                await SevenZip.RunFullAsync(["x", path, "-o" + destination, "-y"], progress);
                break;
            case "tar.gz":
                await ExtractTarGzAsync(path, destination, progress);
                break;
            default:
                await SevenZip.RunAsync(["x", path, "-o" + destination, "-y"], progress);
                break;
        }
    }

    private static async Task ExtractTarGzAsync(string archive, string destination,
                                                ITaskProgress progress)
    {
        // 7-Zip only peels off the gzip layer; the inner .tar has to be
        // extracted in a second pass.
        using var scratch = new ScratchDirectory(destination);
        await SevenZip.RunAsync(["x", archive, "-o" + scratch.Root, "-y"], progress);

        var inner = Directory.EnumerateFiles(scratch.Root).FirstOrDefault()
            ?? throw new InvalidOperationException(Loc.T("msg.archiveNoTar"));

        await SevenZip.RunAsync(["x", inner, "-o" + destination, "-y"], progress);
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