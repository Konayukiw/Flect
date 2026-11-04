namespace Flect.Inspect.Window;

internal sealed class FileAnalyze(Request request) : TaskBase(request)
{
    private FileReport? _report;

    public override string Title => Loc.T("menu.file.analyze");

    public override async Task RunAsync(ITaskProgress progress)
    {
        if (Request.Paths.Count != 1)
        {
            throw new InvalidOperationException(Loc.T("dialog.fileAnalyze.single"));
        }

        progress.Indeterminate();
        progress.Status(Path.GetFileName(Request.Paths[0]));
        _report = await Examine.InspectAsync(Request.Paths[0], progress.Token);
    }

    public override void Present()
    {
        if (_report is not null) new FileAnalyzeWindow(_report).Show();
    }
}
