using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

public sealed record StatTile(string Value, string Label);

public sealed record LargeFileRow(string Size, string Path);

public sealed record TypeRow(string Extension, string Size, string Share, string Count,
                             double BarWidth);

public partial class Analyze : Window
{
    private const double BarMaxWidth = 138;
    private const int MaxEmptyListed = 60;

    internal Analyze(AnalyzeReport report)
    {
        InitializeComponent();
        Title = $"{Branding.Name} — Analyze";

        RootsText.Text = string.Join("   ", report.Roots);

        Tiles.ItemsSource = new[]
        {
            new StatTile(Formatting.Bytes(report.TotalBytes), "Total size"),
            new StatTile(Formatting.Count(report.FileCount), "Files"),
            new StatTile(Formatting.Count(report.DirectoryCount), "Folders"),
            new StatTile($"{report.Elapsed.TotalSeconds:0.0}s", "Scan time"),
        };

        LargestList.ItemsSource = report.Largest
            .Select(file => new LargeFileRow(Formatting.Bytes(file.Size), file.Path))
            .ToList();

        TypeList.ItemsSource = BuildTypeRows(report);

        DuplicateText.Text = report.DuplicateGroups == 0
            ? "None found."
            : $"{Formatting.Plural(report.DuplicateGroups, "group")} · " +
              $"{Formatting.Plural(report.DuplicateFiles, "file")} · " +
              $"{Formatting.Bytes(report.ReclaimableBytes)} reclaimable by keeping one copy of each.";

        EmptyText.Text = report.EmptyDirectories.Count == 0
            ? "None found."
            : $"{Formatting.Plural(report.EmptyDirectories.Count, "folder")}.";
        EmptyList.ItemsSource = report.EmptyDirectories.Take(MaxEmptyListed).ToList();
    }

    private static List<TypeRow> BuildTypeRows(AnalyzeReport report)
    {
        if (report.ByType.Count == 0) return [];

        var shown = report.ByType.Take(AnalyzeReport.TopCount).ToList();
        var rest = report.ByType.Skip(AnalyzeReport.TopCount).ToList();

        double total = Math.Max(1, report.TotalBytes);
        double largest = Math.Max(1, shown[0].Bytes);

        var rows = shown.Select(share => new TypeRow(
            share.Extension,
            Formatting.Bytes(share.Bytes),
            Formatting.Percent(share.Bytes / total),
            Formatting.Plural(share.Count, "file"),
            Math.Max(2, share.Bytes / largest * BarMaxWidth))).ToList();

        if (rest.Count > 0)
        {
            long bytes = rest.Sum(share => share.Bytes);
            rows.Add(new TypeRow(
                $"({rest.Count} more)",
                Formatting.Bytes(bytes),
                Formatting.Percent(bytes / total),
                $"{Formatting.Count(rest.Sum(share => share.Count))} files",
                Math.Max(2, bytes / largest * BarMaxWidth)));
        }
        return rows;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
