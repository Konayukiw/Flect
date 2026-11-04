using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Flect.Folder.Analysis;

internal sealed class Analyze(Request request) : TaskBase(request)
{
    private Analysis? _report;

    public override string Title => Loc.T("menu.folder.analyze");

    public override async Task RunAsync(ITaskProgress progress)
    {
        var folder = Preferences.Current.Folder;
        var stopwatch = Stopwatch.StartNew();

        progress.Indeterminate();
        progress.Status(Loc.T("msg.scanning"));

        var options = ScanOptions.From(folder, folder.Analyze.Exclusions);
        var scan = await Task.Run(() => Scan.ScanTree(Request.Paths, progress, options),
                                  progress.Token);
        var duplicates = await Task.Run(
            () => Scan.FindDuplicates(scan.Files, progress, folder.Duplicates),
            progress.Token);

        _report = Analysis.Build(Request.Paths, scan, duplicates, stopwatch.Elapsed);
    }

    public override void Present()
    {
        if (_report is not null) new AnalyzeWindow(_report).Show();
    }
}
