using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Flect.Folder.Compress;

internal sealed class Compress(Request request) : Flect.Dispatch.Batch(request)
{
    public override string Title => Loc.T("menu.folder.compress");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var format = Preferences.Current.Folder.Archive.CompressFormat;
        var parent = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        var name = Path.GetFileName(path);
        var extension = "." + Type(format);

        progress.Indeterminate();
        progress.Status(Loc.F("label.compress", name));

        using var scratch = new Flect.Files.TempDir(path);
        var staged = scratch.PathFor(name + extension);
        var warnings = new List<string>();

        if (format == ArchiveCompressFormat.TarGz)
        {
            var tar = scratch.PathFor(name + ".tar");
            warnings.AddRange(await SevenZip.RunAsync(["a", "-ttar", tar, name], progress, parent));
            warnings.AddRange(await SevenZip.RunAsync(["a", "-tgzip", staged, Path.GetFileName(tar)],
                                                      progress, scratch.Root));
        }
        else
        {
            warnings.AddRange(await SevenZip.RunAsync(["a", "-t" + Type(format), staged, name],
                                                      progress, parent));
        }

        File.Move(staged, Flect.Files.OutputName.Derive(path, extension: extension));
        SevenZip.Report(warnings, name, progress);
    }

    private static string Type(ArchiveCompressFormat format) => format switch
    {
        ArchiveCompressFormat.SevenZip => "7z",
        ArchiveCompressFormat.Tar => "tar",
        ArchiveCompressFormat.TarGz => "tar.gz",
        _ => "zip",
    };
}
