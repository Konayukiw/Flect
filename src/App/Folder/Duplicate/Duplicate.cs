using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Flect.Folder.Duplicate;

internal sealed class Duplicate(Request request) : TaskBase(request)
{
    private string? _summary;

    public override string Title => Loc.T("menu.folder.removeDuplicate");
    public override string? Summary => _summary;

    public override async Task RunAsync(ITaskProgress progress)
    {
        var folder = Preferences.Current.Folder;

        progress.Indeterminate();
        progress.Status(Loc.T("msg.scanning"));

        var options = ScanOptions.From(folder);
        var scan = await Task.Run(() => Scan.ScanTree(Request.Paths, progress, options),
                                  progress.Token);
        var groups = await Task.Run(
            () => Scan.FindDuplicates(scan.Files, progress, folder.Duplicates),
            progress.Token);

        if (groups.Count == 0)
        {
            _summary = Loc.T("msg.noDuplicates");
            return;
        }

        var mode = Choose(folder.Duplicates.Keep, groups);
        if (mode is null) throw new OperationCanceledException();

        var doomed = Keep.ToDelete(groups, mode.Value);
        if (doomed.Count == 0)
        {
            _summary = Loc.T("msg.nothingToRemove");
            return;
        }

        long reclaimed = doomed.Sum(file => file.Size);
        bool permanent = folder.DeleteMethod == DeleteMethod.Permanent;

        progress.Indeterminate();
        progress.Status(Loc.F(permanent ? "msg.deletingPermanently" : "msg.movingToRecycle",
                              Loc.N("count.file", doomed.Count)));

        var paths = doomed.Select(file => file.Path).ToList();
        await Task.Run(() => Flect.Files.Recycle.Delete(paths, folder.DeleteMethod), progress.Token);

        _summary = Loc.F(permanent ? "msg.deletedPermanently" : "msg.recycled",
                         Loc.N("count.file", doomed.Count), Flect.Display.Format.Bytes(reclaimed));
    }

    private static DuplicateMode? Choose(DuplicateKeep keep, IReadOnlyList<List<ScannedFile>> groups)
        => keep switch
        {
            DuplicateKeep.Oldest => DuplicateMode.KeepOldest,
            DuplicateKeep.Newest => DuplicateMode.KeepNewest,
            _ => DuplicateWindow.Ask(groups),
        };
}
