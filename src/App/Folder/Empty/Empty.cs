using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Flect.Folder.Empty;

internal sealed class Empty(Request request) : TaskBase(request)
{
    private string? _summary;

    public override string Title => Loc.T("menu.folder.removeEmpty");
    public override string? Summary => _summary;

    public override async Task RunAsync(ITaskProgress progress)
    {
        var folder = Preferences.Current.Folder;

        progress.Indeterminate();
        progress.Status(Loc.T("msg.lookingForEmpty"));

        var options = ScanOptions.From(folder);
        var scan = await Task.Run(() => Scan.ScanTree(Request.Paths, progress, options),
                                  progress.Token);
        if (scan.EmptyDirectories.Count == 0)
        {
            _summary = Loc.T("msg.noEmpty");
            return;
        }

        var targets = Scan.Outermost(scan.EmptyDirectories);
        bool permanent = folder.DeleteMethod == DeleteMethod.Permanent;
        var counted = Loc.N("count.emptyFolder", scan.EmptyDirectories.Count);

        var answer = MessageBox.Show(
            Loc.F(permanent ? "msg.confirmEmptyPermanent" : "msg.confirmEmptyRecycle",
                  counted, Preview(targets)),
            Branding.Name, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) throw new OperationCanceledException();

        await Task.Run(() => Flect.Files.Recycle.Delete(targets, folder.DeleteMethod), progress.Token);
        _summary = Loc.F(permanent ? "msg.emptyDeleted" : "msg.emptyRecycled", counted);
    }

    private static string Preview(IReadOnlyList<string> directories)
    {
        const int shown = 12;
        var lines = directories.Take(shown).ToList();
        if (directories.Count > shown)
        {
            lines.Add(Loc.F("msg.andMore", Flect.Display.Format.Count(directories.Count - shown)));
        }
        return string.Join(Environment.NewLine, lines);
    }
}
