using System.Windows;
using Microsoft.Win32;

namespace Optimizer.Gui.Dialogs;

public partial class FileAnalyzeWindow : Window
{
    private readonly FileReport _report;
    private readonly CancellationTokenSource _hashCancel = new();
    private readonly Task<string> _hash;

    internal FileAnalyzeWindow(FileReport report)
    {
        _report = report;
        InitializeComponent();
        Title = $"{Branding.Name} - {Loc.T("dialog.fileAnalyze.title")}";
        PathText.Text = report.Path;
        AlertList.ItemsSource = report.Alerts;
        OverviewList.ItemsSource = report.Overview;
        SectionList.ItemsSource = report.Sections;
        HashText.Text = Loc.T("dialog.fileAnalyze.hashPending");
        _hash = Task.Run(() => FileHasher.Sha256(report.Path, _hashCancel.Token));
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            HashText.Text = await _hash;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HashText.Text = ex.Message;
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var format = Settings.Current.Folder.Analyze.SaveFormat;
        var dialog = new SaveFileDialog
        {
            FileName = $"{Branding.Name}-file{ReportWriter.Extension(format)}",
            DefaultExt = ReportWriter.Extension(format),
            Filter = ReportWriter.Filter(format),
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            string? hash = null;
            if (_hash.IsCompletedSuccessfully) hash = _hash.Result;
            else
            {
                HashText.Text = Loc.T("dialog.fileAnalyze.hashPending");
                hash = await _hash;
                HashText.Text = hash;
            }

            FileReportWriter.Write(_report, hash, dialog.FileName, format);
            Report.Info(Loc.F("dialog.analyze.saved", dialog.FileName));
        }
        catch (Exception ex)
        {
            Report.Error(Loc.F("dialog.analyze.saveFailed", ex.Message));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hashCancel.Cancel();
        base.OnClosed(e);
    }
}
