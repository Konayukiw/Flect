using System.Windows;
using Microsoft.Win32;
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

    private readonly AnalyzeReport _report;

    internal Analyze(AnalyzeReport report)
    {
        _report = report;

        InitializeComponent();
        Title = $"{Branding.Name} — {Loc.T("dialog.analyze.title")}";

        RootsText.Text = string.Join("   ", report.Roots);

        Tiles.ItemsSource = new[]
        {
            new StatTile(Formatting.Bytes(report.TotalBytes), Loc.T("dialog.analyze.total")),
            new StatTile(Formatting.Count(report.FileCount), Loc.T("dialog.analyze.files")),
            new StatTile(Formatting.Count(report.DirectoryCount), Loc.T("dialog.analyze.folders")),
            new StatTile($"{report.Elapsed.TotalSeconds:0.0}s", Loc.T("dialog.analyze.scanTime")),
        };

        LargestList.ItemsSource = report.Largest
            .Select(file => new LargeFileRow(Formatting.Bytes(file.Size), file.Path))
            .ToList();

        TypeList.ItemsSource = BuildTypeRows(report);

        DuplicateText.Text = report.DuplicateGroups == 0
            ? Loc.T("dialog.analyze.none")
            : Loc.F("dialog.analyze.dupSummary",
                    Loc.N("count.group", report.DuplicateGroups),
                    Loc.N("count.file", report.DuplicateFiles),
                    Formatting.Bytes(report.ReclaimableBytes));

        EmptyText.Text = report.EmptyDirectories.Count == 0
            ? Loc.T("dialog.analyze.none")
            : Loc.N("count.folder", report.EmptyDirectories.Count);
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
            Loc.N("count.file", share.Count),
            Math.Max(2, share.Bytes / largest * BarMaxWidth))).ToList();

        if (rest.Count > 0)
        {
            long bytes = rest.Sum(share => share.Bytes);
            rows.Add(new TypeRow(
                Loc.F("dialog.analyze.moreTypes", Formatting.Count(rest.Count)),
                Formatting.Bytes(bytes),
                Formatting.Percent(bytes / total),
                Loc.N("count.file", rest.Sum(share => share.Count)),
                Math.Max(2, bytes / largest * BarMaxWidth)));
        }
        return rows;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var format = Settings.Current.Folder.Analyze.SaveFormat;

        var dialog = new SaveFileDialog
        {
            FileName = $"{Branding.Name}-analyze{ReportWriter.Extension(format)}",
            DefaultExt = ReportWriter.Extension(format),
            Filter = ReportWriter.Filter(format),
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ReportWriter.Write(_report, dialog.FileName, format);
            Report.Info(Loc.F("dialog.analyze.saved", dialog.FileName));
        }
        catch (Exception ex)
        {
            Report.Error(Loc.F("dialog.analyze.saveFailed", ex.Message));
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
