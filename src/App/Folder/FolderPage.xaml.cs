using System.Windows;
using System.Windows.Controls;

namespace Flect.Folder;

public partial class FolderPage : UserControl, ISettings
{
    public FolderPage()
    {
        InitializeComponent();

        Flect.Settings.Binding.Fill(KeepBox,
            (DuplicateKeep.Ask, "settings.folder.dup.keep.ask"),
            (DuplicateKeep.Oldest, "settings.folder.dup.keep.oldest"),
            (DuplicateKeep.Newest, "settings.folder.dup.keep.newest"));

        Flect.Settings.Binding.FillRaw(ReportFormatBox,
            (ReportFormat.Csv, "CSV"),
            (ReportFormat.Json, "JSON"),
            (ReportFormat.Html, "HTML"),
            (ReportFormat.Txt, "TXT"));

        Flect.Settings.Binding.Fill(CompressFormatBox,
            (ArchiveCompressFormat.Zip, "settings.folder.compress.format.zip"),
            (ArchiveCompressFormat.SevenZip, "settings.folder.compress.format.sevenzip"),
            (ArchiveCompressFormat.Tar, "settings.folder.compress.format.tar"),
            (ArchiveCompressFormat.TarGz, "settings.folder.compress.format.targz"));
    }

    void ISettings.Load(Preferences settings)
    {
        var folder = settings.Folder;
        var duplicates = folder.Duplicates;

        StrictSizeBox.IsChecked = duplicates.Strictness == DuplicateStrictness.SizeOnly;
        StrictHeadBox.IsChecked = duplicates.Strictness == DuplicateStrictness.HeadHash;
        StrictFullBox.IsChecked = duplicates.Strictness == DuplicateStrictness.FullContent;

        MatchNameBox.IsChecked = duplicates.MatchName;
        MatchTimeBox.IsChecked = duplicates.MatchTimestamp;
        IgnoreEmptyBox.IsChecked = duplicates.IgnoreEmptyFiles;
        Flect.Settings.Binding.ShowOptionalBytes(MinimumBytesBox, duplicates.MinimumBytes);
        Flect.Settings.Binding.Select(KeepBox, duplicates.Keep);

        RecycleBox.IsChecked = folder.DeleteMethod == DeleteMethod.RecycleBin;
        PermanentBox.IsChecked = folder.DeleteMethod == DeleteMethod.Permanent;

        ScanExclusions.Value = folder.ScanExclusions;

        NumberingBox.IsChecked = folder.Rename.UseNumbering;
        Flect.Settings.Binding.ShowInt(DigitsBox, folder.Rename.DigitCount);
        Flect.Settings.Binding.ShowInt(StartNumberBox, folder.Rename.StartNumber);
        DryRunBox.IsChecked = folder.Rename.DryRun;
        RenameExclusions.Value = folder.Rename.Exclusions;

        EmptyZeroBox.IsChecked = folder.Empty.IgnoreZeroByteFiles;
        EmptyDesktopIniBox.IsChecked = folder.Empty.IgnoreDesktopIni;
        EmptyThumbsBox.IsChecked = folder.Empty.IgnoreThumbsDb;

        FoldersOnlyBox.IsChecked = folder.Tree.FoldersOnly;
        TreeExclusions.Value = folder.Tree.Exclusions;

        Flect.Settings.Binding.Select(ReportFormatBox, folder.Analyze.SaveFormat);
        AnalyzeExclusions.Value = folder.Analyze.Exclusions;

        Flect.Settings.Binding.Select(CompressFormatBox, folder.Archive.CompressFormat);
    }

    void ISettings.Store(Preferences settings)
    {
        var folder = settings.Folder;
        var duplicates = folder.Duplicates;

        duplicates.Strictness = StrictSizeBox.IsChecked is true ? DuplicateStrictness.SizeOnly
                              : StrictHeadBox.IsChecked is true ? DuplicateStrictness.HeadHash
                              : DuplicateStrictness.FullContent;

        duplicates.MatchName = MatchNameBox.IsChecked is true;
        duplicates.MatchTimestamp = MatchTimeBox.IsChecked is true;
        duplicates.IgnoreEmptyFiles = IgnoreEmptyBox.IsChecked is true;
        duplicates.MinimumBytes = Flect.Settings.Binding.ReadOptionalBytes(MinimumBytesBox, duplicates.MinimumBytes);
        duplicates.Keep = Flect.Settings.Binding.Selected(KeepBox, duplicates.Keep);

        folder.DeleteMethod = PermanentBox.IsChecked is true
            ? DeleteMethod.Permanent
            : DeleteMethod.RecycleBin;

        folder.ScanExclusions = ScanExclusions.Value;

        folder.Rename.UseNumbering = NumberingBox.IsChecked is true;
        folder.Rename.DigitCount = Flect.Settings.Binding.Int(DigitsBox, folder.Rename.DigitCount, 0, 12);
        folder.Rename.StartNumber = Flect.Settings.Binding.Int(StartNumberBox, folder.Rename.StartNumber, 0, 1000000);
        folder.Rename.DryRun = DryRunBox.IsChecked is true;
        folder.Rename.Exclusions = RenameExclusions.Value;

        folder.Empty.IgnoreZeroByteFiles = EmptyZeroBox.IsChecked is true;
        folder.Empty.IgnoreDesktopIni = EmptyDesktopIniBox.IsChecked is true;
        folder.Empty.IgnoreThumbsDb = EmptyThumbsBox.IsChecked is true;

        folder.Tree.FoldersOnly = FoldersOnlyBox.IsChecked is true;
        folder.Tree.Exclusions = TreeExclusions.Value;

        folder.Analyze.SaveFormat = Flect.Settings.Binding.Selected(ReportFormatBox, folder.Analyze.SaveFormat);
        folder.Analyze.Exclusions = AnalyzeExclusions.Value;

        folder.Archive.CompressFormat =
            Flect.Settings.Binding.Selected(CompressFormatBox, folder.Archive.CompressFormat);
    }

    private void OnDeleteModeChanged(object sender, RoutedEventArgs e)
    {
        if (PermanentWarning is null) return;
        PermanentWarning.Visibility = PermanentBox.IsChecked is true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
