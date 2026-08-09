using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Optimizer.Main;
using Optimizer.Gui;

namespace Optimizer.Tasks;

internal sealed class FolderRemoveDuplicate(TaskRequest request) : TaskBase(request)
{
    private string? _summary;

    public override string Title => Loc.T("menu.folder.removeDuplicate");
    public override string? Summary => _summary;

    public override async Task RunAsync(ITaskProgress progress)
    {
        var folder = Settings.Current.Folder;

        progress.Indeterminate();
        progress.Status(Loc.T("msg.scanning"));

        var options = ScanOptions.From(folder);
        var scan = await Task.Run(() => FileScanner.Scan(Request.Paths, progress, options),
                                  progress.Token);
        var groups = await Task.Run(
            () => FileScanner.FindDuplicates(scan.Files, progress, folder.Duplicates),
            progress.Token);

        if (groups.Count == 0)
        {
            _summary = Loc.T("msg.noDuplicates");
            return;
        }

        var mode = Choose(folder.Duplicates.Keep, groups);
        if (mode is null) throw new OperationCanceledException();

        var doomed = DuplicatePlan.ToDelete(groups, mode.Value);
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
        await Task.Run(() => Recycler.Delete(paths, folder.DeleteMethod), progress.Token);

        _summary = Loc.F(permanent ? "msg.deletedPermanently" : "msg.recycled",
                         Loc.N("count.file", doomed.Count), Formatting.Bytes(reclaimed));
    }

    private static DuplicateMode? Choose(DuplicateKeep keep, IReadOnlyList<List<ScannedFile>> groups)
        => keep switch
        {
            DuplicateKeep.Oldest => DuplicateMode.KeepOldest,
            DuplicateKeep.Newest => DuplicateMode.KeepNewest,
            _ => Duplicate.Ask(groups),
        };
}

internal sealed class FolderRemoveEmpty(TaskRequest request) : TaskBase(request)
{
    private string? _summary;

    public override string Title => Loc.T("menu.folder.removeEmpty");
    public override string? Summary => _summary;

    public override async Task RunAsync(ITaskProgress progress)
    {
        var folder = Settings.Current.Folder;

        progress.Indeterminate();
        progress.Status(Loc.T("msg.lookingForEmpty"));

        var options = ScanOptions.From(folder);
        var scan = await Task.Run(() => FileScanner.Scan(Request.Paths, progress, options),
                                  progress.Token);
        if (scan.EmptyDirectories.Count == 0)
        {
            _summary = Loc.T("msg.noEmpty");
            return;
        }

        var targets = FileScanner.Outermost(scan.EmptyDirectories);
        bool permanent = folder.DeleteMethod == DeleteMethod.Permanent;
        var counted = Loc.N("count.emptyFolder", scan.EmptyDirectories.Count);

        var answer = MessageBox.Show(
            Loc.F(permanent ? "msg.confirmEmptyPermanent" : "msg.confirmEmptyRecycle",
                  counted, Preview(targets)),
            Branding.Name, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) throw new OperationCanceledException();

        await Task.Run(() => Recycler.Delete(targets, folder.DeleteMethod), progress.Token);
        _summary = Loc.F(permanent ? "msg.emptyDeleted" : "msg.emptyRecycled", counted);
    }

    private static string Preview(IReadOnlyList<string> directories)
    {
        const int shown = 12;
        var lines = directories.Take(shown).ToList();
        if (directories.Count > shown)
        {
            lines.Add(Loc.F("msg.andMore", Formatting.Count(directories.Count - shown)));
        }
        return string.Join(Environment.NewLine, lines);
    }
}

internal sealed class FolderTree(TaskRequest request) : TaskBase(request)
{
    private string _output = string.Empty;

    public override string Title => Loc.T("menu.folder.tree");

    public override Task RunAsync(ITaskProgress progress) => Task.Run(() =>
    {
        progress.Indeterminate();
        _output = TreeRenderer.Render(Request.Paths, Settings.Current.Folder.Tree, progress);
    }, progress.Token);

    public override void Present() => new TextResult(Title, _output).Show();
}

internal sealed class FolderRename(TaskRequest request) : TaskBase(request)
{
    private RenamePlan _plan = new(string.Empty, string.Empty, string.Empty, true);
    private RenameSettings _settings = new();
    private ExclusionFilter _exclusions = ExclusionFilter.None;
    private readonly List<string> _preview = [];
    private int _renamed;

    public override string Title => Loc.T("menu.folder.rename");

    public override string? Summary => _settings.DryRun
        ? Loc.F("msg.renameDryRun", Loc.N("count.file", _preview.Count))
        : Loc.F("msg.renamed", Loc.N("count.file", _renamed));

    public override bool Configure()
    {
        _settings = Settings.Current.Folder.Rename;
        _exclusions = ExclusionFilter.From(_settings.Exclusions);

        var sample = Request.Paths
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder))
            .Select(Path.GetFileName)
            .Where(name => name is not null && !_exclusions.ExcludesFile(name))
            .Select(Path.GetExtension)
            .FirstOrDefault(extension => !string.IsNullOrEmpty(extension));

        var answer = Rename.Ask(sample ?? ".png");
        if (answer is null) return false;

        _plan = answer;
        return true;
    }

    public override Task RunAsync(ITaskProgress progress) => Task.Run(() =>
    {
        for (int index = 0; index < Request.Paths.Count; index++)
        {
            progress.Token.ThrowIfCancellationRequested();
            progress.Step(index, Request.Paths.Count);

            var folder = Request.Paths[index];
            if (!Directory.Exists(folder)) continue;

            progress.Status(folder);
            RenameFolder(folder, progress);
        }
        progress.Step(Request.Paths.Count, Request.Paths.Count);
    }, progress.Token);

    public override void Present()
    {
        if (!_settings.DryRun || _preview.Count == 0) return;
        new TextResult(Loc.T("msg.renameDryRunTitle"), string.Join(Environment.NewLine, _preview))
            .Show();
    }

    private void RenameFolder(string folder, ITaskProgress progress)
    {
        var groups = new DirectoryInfo(folder).EnumerateFiles()
            .Where(file => !_exclusions.ExcludesFile(file.Name))
            .GroupBy(file => file.Extension.ToLowerInvariant());

        foreach (var group in groups)
        {
            var files = group.OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                             .ToList();

            int highest = _settings.StartNumber + files.Count - 1;
            int width = _settings.DigitCount > 0
                ? _settings.DigitCount
                : Math.Max(1, highest.ToString().Length);

            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var plan = new List<(FileInfo Source, string Target)>();
            for (int index = 0; index < files.Count; index++)
            {
                var middle = _plan.Numbered
                    ? (_settings.StartNumber + index).ToString().PadLeft(width, '0')
                    : _plan.Name;

                var stem = $"{_plan.Prefix}{middle}{_plan.Suffix}";
                var extension = files[index].Extension;

                var name = stem + extension;
                for (int n = 2; !taken.Add(name); n++) name = $"{stem} ({n}){extension}";

                plan.Add((files[index], Path.Combine(folder, name)));
            }

            if (_settings.DryRun)
            {
                foreach (var (source, target) in plan)
                {
                    _preview.Add($"{source.Name}  ->  {Path.GetFileName(target)}");
                }
                continue;
            }
            Apply(plan, progress);
        }
    }

    private void Apply(List<(FileInfo Source, string Target)> plan, ITaskProgress progress)
    {
        var staged = new List<(string Scratch, string Target, string Original)>();

        foreach (var (source, target) in plan)
        {
            progress.Token.ThrowIfCancellationRequested();
            if (string.Equals(source.FullName, target, StringComparison.OrdinalIgnoreCase)) continue;

            var scratch = OutputPath.Scratch(target, source.Extension);
            try
            {
                var original = source.FullName;
                source.MoveTo(scratch);
                staged.Add((scratch, target, original));
            }
            catch (Exception ex)
            {
                progress.Error($"{source.Name} — {ex.Message}");
            }
        }

        foreach (var (scratch, target, original) in staged)
        {
            try
            {
                File.Move(scratch, target);
                _renamed++;
            }
            catch (Exception ex)
            {
                progress.Error($"{Path.GetFileName(target)} — {ex.Message}");
                try { File.Move(scratch, original); }
                catch (IOException) { progress.Error($"Left behind as {Path.GetFileName(scratch)}"); }
            }
        }
    }
}

internal sealed class FolderAnalyze(TaskRequest request) : TaskBase(request)
{
    private AnalyzeReport? _report;

    public override string Title => Loc.T("menu.folder.analyze");

    public override async Task RunAsync(ITaskProgress progress)
    {
        var folder = Settings.Current.Folder;
        var stopwatch = Stopwatch.StartNew();

        progress.Indeterminate();
        progress.Status(Loc.T("msg.scanning"));

        var options = ScanOptions.From(folder, folder.Analyze.Exclusions);
        var scan = await Task.Run(() => FileScanner.Scan(Request.Paths, progress, options),
                                  progress.Token);
        var duplicates = await Task.Run(
            () => FileScanner.FindDuplicates(scan.Files, progress, folder.Duplicates),
            progress.Token);

        _report = AnalyzeReport.Build(Request.Paths, scan, duplicates, stopwatch.Elapsed);
    }

    public override void Present()
    {
        if (_report is not null) new Analyze(_report).Show();
    }
}
