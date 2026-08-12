using System.Windows;
using System.Windows.Controls;

namespace Optimizer.Gui.Pages;

public partial class Folder : UserControl, ISettingsPage
{
    public Folder()
    {
        InitializeComponent();

        Fields.Fill(KeepBox,
            (DuplicateKeep.Ask, "settings.folder.dup.keep.ask"),
            (DuplicateKeep.Oldest, "settings.folder.dup.keep.oldest"),
            (DuplicateKeep.Newest, "settings.folder.dup.keep.newest"));

        Fields.FillRaw(ReportFormatBox,
            (ReportFormat.Csv, "CSV"),
            (ReportFormat.Json, "JSON"),
            (ReportFormat.Html, "HTML"),
            (ReportFormat.Txt, "TXT"));
    }

    void ISettingsPage.Load(Settings settings)
    {
        var folder = settings.Folder;
        var duplicates = folder.Duplicates;

        StrictSizeBox.IsChecked = duplicates.Strictness == DuplicateStrictness.SizeOnly;
        StrictHeadBox.IsChecked = duplicates.Strictness == DuplicateStrictness.HeadHash;
        StrictFullBox.IsChecked = duplicates.Strictness == DuplicateStrictness.FullContent;

        MatchNameBox.IsChecked = duplicates.MatchName;
        MatchTimeBox.IsChecked = duplicates.MatchTimestamp;
        IgnoreEmptyBox.IsChecked = duplicates.IgnoreEmptyFiles;
        Fields.ShowOptionalBytes(MinimumBytesBox, duplicates.MinimumBytes);
        Fields.Select(KeepBox, duplicates.Keep);

        RecycleBox.IsChecked = folder.DeleteMethod == DeleteMethod.RecycleBin;
        PermanentBox.IsChecked = folder.DeleteMethod == DeleteMethod.Permanent;

        ScanExclusions.Value = folder.ScanExclusions;

        NumberingBox.IsChecked = folder.Rename.UseNumbering;
        Fields.ShowInt(DigitsBox, folder.Rename.DigitCount);
        Fields.ShowInt(StartNumberBox, folder.Rename.StartNumber);
        DryRunBox.IsChecked = folder.Rename.DryRun;
        RenameExclusions.Value = folder.Rename.Exclusions;

        EmptyZeroBox.IsChecked = folder.Empty.IgnoreZeroByteFiles;
        EmptyDesktopIniBox.IsChecked = folder.Empty.IgnoreDesktopIni;
        EmptyThumbsBox.IsChecked = folder.Empty.IgnoreThumbsDb;

        FoldersOnlyBox.IsChecked = folder.Tree.FoldersOnly;
        TreeExclusions.Value = folder.Tree.Exclusions;

        Fields.Select(ReportFormatBox, folder.Analyze.SaveFormat);
        AnalyzeExclusions.Value = folder.Analyze.Exclusions;
    }

    void ISettingsPage.Store(Settings settings)
    {
        var folder = settings.Folder;
        var duplicates = folder.Duplicates;

        duplicates.Strictness = StrictSizeBox.IsChecked == true ? DuplicateStrictness.SizeOnly
                              : StrictHeadBox.IsChecked == true ? DuplicateStrictness.HeadHash
                              : DuplicateStrictness.FullContent;

        duplicates.MatchName = MatchNameBox.IsChecked == true;
        duplicates.MatchTimestamp = MatchTimeBox.IsChecked == true;
        duplicates.IgnoreEmptyFiles = IgnoreEmptyBox.IsChecked == true;
        duplicates.MinimumBytes = Fields.ReadOptionalBytes(MinimumBytesBox, duplicates.MinimumBytes);
        duplicates.Keep = Fields.Selected(KeepBox, duplicates.Keep);

        folder.DeleteMethod = PermanentBox.IsChecked == true
            ? DeleteMethod.Permanent
            : DeleteMethod.RecycleBin;

        folder.ScanExclusions = ScanExclusions.Value;

        folder.Rename.UseNumbering = NumberingBox.IsChecked == true;
        folder.Rename.DigitCount = Fields.Int(DigitsBox, folder.Rename.DigitCount, 0, 12);
        folder.Rename.StartNumber = Fields.Int(StartNumberBox, folder.Rename.StartNumber, 0, 1000000);
        folder.Rename.DryRun = DryRunBox.IsChecked == true;
        folder.Rename.Exclusions = RenameExclusions.Value;

        folder.Empty.IgnoreZeroByteFiles = EmptyZeroBox.IsChecked == true;
        folder.Empty.IgnoreDesktopIni = EmptyDesktopIniBox.IsChecked == true;
        folder.Empty.IgnoreThumbsDb = EmptyThumbsBox.IsChecked == true;

        folder.Tree.FoldersOnly = FoldersOnlyBox.IsChecked == true;
        folder.Tree.Exclusions = TreeExclusions.Value;

        folder.Analyze.SaveFormat = Fields.Selected(ReportFormatBox, folder.Analyze.SaveFormat);
        folder.Analyze.Exclusions = AnalyzeExclusions.Value;
    }

    private void OnDeleteModeChanged(object sender, RoutedEventArgs e)
    {
        if (PermanentWarning is null) return;
        PermanentWarning.Visibility = PermanentBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
