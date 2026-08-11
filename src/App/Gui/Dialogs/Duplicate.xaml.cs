using System.Collections.ObjectModel;
using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

public sealed record DuplicateRow(string Note, string Text, bool IsHeader, bool IsRemoving);

public partial class Duplicate : Window
{
    private readonly IReadOnlyList<List<ScannedFile>> _groups;
    private readonly ObservableCollection<DuplicateRow> _rows = [];

    private bool _ready;

    private Duplicate(IReadOnlyList<List<ScannedFile>> groups)
    {
        _groups = groups;
        InitializeComponent();
        Title = $"{Branding.Name} — {Loc.T("menu.folder.removeDuplicate")}";
        RowList.ItemsSource = _rows;

        AcceptButton.Content = Settings.Current.Folder.DeleteMethod == DeleteMethod.Permanent
            ? Loc.T("dialog.duplicate.delete")
            : Loc.T("dialog.duplicate.recycle");

        int copies = groups.Sum(group => group.Count);
        long reclaimable = groups.Sum(group => group.Sum(file => file.Size) - group[0].Size);
        FoundText.Text = Loc.F("dialog.duplicate.found",
                               Loc.N("count.group", groups.Count),
                               Loc.N("count.file", copies),
                               Formatting.Bytes(reclaimable));

        _ready = true;
        Refresh();
    }

    internal static DuplicateMode? Ask(IReadOnlyList<List<ScannedFile>> groups)
    {
        var dialog = new Duplicate(groups);
        return dialog.ShowDialog() == true ? dialog.SelectedMode : null;
    }

    private DuplicateMode SelectedMode =>
        DeleteAllOption.IsChecked == true ? DuplicateMode.DeleteAll
        : KeepNewestOption.IsChecked == true ? DuplicateMode.KeepNewest
        : DuplicateMode.KeepOldest;

    private void OnModeChanged(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (!_ready) return;

        var doomed = DuplicatePlan.ToDelete(_groups, SelectedMode)
                                  .Select(file => file.Path)
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _rows.Clear();
        foreach (var group in _groups)
        {
            _rows.Add(new DuplicateRow(
                string.Empty,
                Loc.F("dialog.duplicate.groupHeader",
                      Loc.N("count.identical", group.Count),
                      Formatting.Bytes(group[0].Size)),
                IsHeader: true, IsRemoving: false));

            foreach (var file in group.OrderBy(file => file.LastWrite))
            {
                bool removing = doomed.Contains(file.Path);
                _rows.Add(new DuplicateRow(
                    Loc.T(removing ? "dialog.duplicate.remove" : "dialog.duplicate.keep"),
                    file.Path, IsHeader: false, removing));
            }
        }

        long freed = _groups.SelectMany(group => group)
                            .Where(file => doomed.Contains(file.Path))
                            .Sum(file => file.Size);
        PlanText.Text = $"{Loc.N("count.file", doomed.Count)} · {Formatting.Bytes(freed)}";
        AcceptButton.IsEnabled = doomed.Count > 0;
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
