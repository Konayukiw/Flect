using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Flect.Folder.Rename;

internal sealed class Rename(Request request) : TaskBase(request)
{
    private RenamePlan _plan = new(string.Empty, string.Empty, string.Empty, true);
    private RenameSettings _settings = new();
    private Exclusion _exclusions = Exclusion.None;
    private readonly List<string> _preview = [];
    private int _renamed;

    public override string Title => Loc.T("menu.folder.rename");

    public override string? Summary => _settings.DryRun
        ? Loc.F("msg.renameDryRun", Loc.N("count.file", _preview.Count))
        : Loc.F("msg.renamed", Loc.N("count.file", _renamed));

    public override bool Configure()
    {
        _settings = Preferences.Current.Folder.Rename;
        _exclusions = Exclusion.From(_settings.Exclusions);

        var sample = Request.Paths
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder))
            .Select(Path.GetFileName)
            .Where(name => name is not null && !_exclusions.ExcludesFile(name))
            .Select(Path.GetExtension)
            .FirstOrDefault(extension => !string.IsNullOrEmpty(extension));

        var answer = RenameWindow.Ask(sample ?? ".png");
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
        new Flect.Text.Result(Loc.T("msg.renameDryRunTitle"), string.Join(Environment.NewLine, _preview))
            .Show();
    }

    private void RenameFolder(string folder, ITaskProgress progress)
    {
        var groups = new DirectoryInfo(folder).EnumerateFiles()
            .Where(file => !_exclusions.ExcludesFile(file.Name))
            .GroupBy(file => file.Extension.ToLowerInvariant());

        foreach (var group in groups)
        {
            var files = group.OrderBy(file => file.Name, Flect.Files.ExplorerSort.Instance)
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

            var scratch = Flect.Files.OutputName.Scratch(target, source.Extension);
            try
            {
                var original = source.FullName;
                source.MoveTo(scratch);
                staged.Add((scratch, target, original));
            }
            catch (Exception ex)
            {
                progress.Error($"{source.Name} - {ex.Message}");
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
                progress.Error($"{Path.GetFileName(target)} - {ex.Message}");
                try { File.Move(scratch, original); }
                catch (IOException) { progress.Error($"Left behind as {Path.GetFileName(scratch)}"); }
            }
        }
    }
}
