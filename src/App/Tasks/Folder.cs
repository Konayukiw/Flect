using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Optimizer.Main;
using Optimizer.Gui;

namespace Optimizer.Tasks;

internal sealed class FolderRemoveDuplicate(TaskRequest request) : TaskBase(request)
{
    private string? _summary;

    public override string Title => "Remove Duplicate";
    public override string? Summary => _summary;

    public override async Task RunAsync(ITaskProgress progress)
    {
        progress.Indeterminate();
        progress.Status("Scanning...");

        var scan = await Task.Run(() => FileScanner.Scan(Request.Paths, progress), progress.Token);
        var groups = await Task.Run(() => FileScanner.FindDuplicates(scan.Files, progress),
                                    progress.Token);

        if (groups.Count == 0)
        {
            _summary = "No duplicate files found.";
            return;
        }

        var mode = DuplicateDialog.Ask(groups);
        if (mode is null) throw new OperationCanceledException();

        var doomed = DuplicatePlan.ToDelete(groups, mode.Value);
        if (doomed.Count == 0)
        {
            _summary = "Nothing to remove.";
            return;
        }

        long reclaimed = doomed.Sum(file => file.Size);
        progress.Indeterminate();
        progress.Status($"Moving {Formatting.Count(doomed.Count)} files to the Recycle Bin");

        var paths = doomed.Select(file => file.Path).ToList();
        await Task.Run(() => Recycler.Delete(paths), progress.Token);

        _summary = $"Moved {Formatting.Count(doomed.Count)} file(s) to the Recycle Bin, " +
                   $"reclaiming {Formatting.Bytes(reclaimed)}.";
    }
}

internal sealed class FolderRemoveEmpty(TaskRequest request) : TaskBase(request)
{
    private string? _summary;

    public override string Title => "Remove Empty";
    public override string? Summary => _summary;

    public override async Task RunAsync(ITaskProgress progress)
    {
        progress.Indeterminate();
        progress.Status("Looking for empty folders...");

        var scan = await Task.Run(() => FileScanner.Scan(Request.Paths, progress), progress.Token);
        if (scan.EmptyDirectories.Count == 0)
        {
            _summary = "No empty folders found.";
            return;
        }

        var targets = FileScanner.Outermost(scan.EmptyDirectories);

        var answer = MessageBox.Show(
            $"Move {Formatting.Count(scan.EmptyDirectories.Count)} empty folder(s) " +
            "to the Recycle Bin?\n\n" + Preview(targets),
            Branding.Name, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) throw new OperationCanceledException();

        await Task.Run(() => Recycler.Delete(targets), progress.Token);
        _summary = $"Moved {Formatting.Count(scan.EmptyDirectories.Count)} empty folder(s) " +
                   "to the Recycle Bin.";
    }

    private static string Preview(IReadOnlyList<string> directories)
    {
        const int shown = 12;
        var lines = directories.Take(shown).ToList();
        if (directories.Count > shown)
        {
            lines.Add($"...and {Formatting.Count(directories.Count - shown)} more");
        }
        return string.Join(Environment.NewLine, lines);
    }
}

internal sealed class FolderTree(TaskRequest request) : TaskBase(request)
{
    private string _output = string.Empty;

    public override string Title => "tree /f";

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    public override async Task RunAsync(ITaskProgress progress)
    {
        progress.Indeterminate();

        var console = Encoding.GetEncoding((int)GetOEMCP());
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "tree.com");

        var builder = new StringBuilder();
        foreach (var folder in Request.Paths)
        {
            progress.Token.ThrowIfCancellationRequested();
            progress.Status(folder);

            if (builder.Length > 0) builder.AppendLine().AppendLine();

            var result = await ProcessRunner.RunAsync(executable, [folder, "/f"], progress.Token,
                                                     console);
            builder.Append(result.StandardOutput.TrimEnd());
            if (!result.Succeeded && result.StandardError.Trim().Length > 0)
            {
                builder.AppendLine().Append(result.StandardError.TrimEnd());
            }
        }
        _output = builder.ToString();
    }

    public override void Present() => new TextResultWindow("tree /f", _output).Show();
}

internal sealed class FolderRename(TaskRequest request) : TaskBase(request)
{
    private RenamePlan _plan = new(string.Empty, string.Empty, string.Empty, true);
    private int _renamed;

    public override string Title => "Rename";
    public override string? Summary => $"Renamed {Formatting.Count(_renamed)} file(s).";

    public override bool Configure()
    {
        var sample = Request.Paths
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder))
            .Select(Path.GetExtension)
            .FirstOrDefault(extension => !string.IsNullOrEmpty(extension));

        var answer = RenameDialog.Ask(sample ?? ".png");
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

    private void RenameFolder(string folder, ITaskProgress progress)
    {
        var groups = new DirectoryInfo(folder).EnumerateFiles()
            .GroupBy(file => file.Extension.ToLowerInvariant());

        foreach (var group in groups)
        {
            var files = group.OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
                             .ToList();
            int width = files.Count.ToString().Length;

            // Without a counter every file in the group wants the same name, so the
            // taken ones are tracked and later duplicates pick up " (2)", " (3)" the
            // way the rest of the program disambiguates.
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var plan = new List<(FileInfo Source, string Target)>();
            for (int index = 0; index < files.Count; index++)
            {
                var middle = _plan.Numbered
                    ? (index + 1).ToString().PadLeft(width, '0')
                    : _plan.Name;

                var stem = $"{_plan.Prefix}{middle}{_plan.Suffix}";
                var extension = files[index].Extension;

                var name = stem + extension;
                for (int n = 2; !taken.Add(name); n++) name = $"{stem} ({n}){extension}";

                plan.Add((files[index], Path.Combine(folder, name)));
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

    public override string Title => "Analyze";

    public override async Task RunAsync(ITaskProgress progress)
    {
        var stopwatch = Stopwatch.StartNew();
        progress.Indeterminate();
        progress.Status("Scanning...");

        var scan = await Task.Run(() => FileScanner.Scan(Request.Paths, progress), progress.Token);
        var duplicates = await Task.Run(() => FileScanner.FindDuplicates(scan.Files, progress),
                                        progress.Token);

        _report = AnalyzeReport.Build(Request.Paths, scan, duplicates, stopwatch.Elapsed);
    }

    public override void Present()
    {
        if (_report is not null) new AnalyzeWindow(_report).Show();
    }
}
