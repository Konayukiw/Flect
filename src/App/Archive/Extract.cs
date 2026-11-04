using System.Diagnostics;

namespace Flect.Archive;

internal sealed class Extract(Request request) : Flect.Dispatch.Batch(request)
{
    public override string Title => Loc.T("menu.archive.extract");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        progress.Indeterminate();
        progress.Status(Loc.F("label.extracting", Path.GetFileName(path)));

        var destination = Flect.Files.OutputName.DeriveDirectory(path, ArchiveStem(path));

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
        using var scratch = new Flect.Files.TempDir(destination);
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
