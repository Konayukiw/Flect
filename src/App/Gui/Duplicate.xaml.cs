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
        Title = $"{Branding.Name} — Remove Duplicate";
        RowList.ItemsSource = _rows;

        int copies = groups.Sum(group => group.Count);
        long reclaimable = groups.Sum(group => group.Sum(file => file.Size) - group[0].Size);
        FoundText.Text =
            $"{Formatting.Plural(groups.Count, "group")}, {Formatting.Plural(copies, "file")}, " +
            $"up to {Formatting.Bytes(reclaimable)} reclaimable.";

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
        int index = 0;
        foreach (var group in _groups)
        {
            index++;
            _rows.Add(new DuplicateRow(
                string.Empty,
                $"{Formatting.Plural(group.Count, "identical file")} · " +
                $"{Formatting.Bytes(group[0].Size)} each",
                IsHeader: true, IsRemoving: false));

            foreach (var file in group.OrderBy(file => file.LastWrite))
            {
                bool removing = doomed.Contains(file.Path);
                _rows.Add(new DuplicateRow(removing ? "Remove" : "Keep", file.Path,
                                           IsHeader: false, removing));
            }
        }

        long freed = _groups.SelectMany(group => group)
                            .Where(file => doomed.Contains(file.Path))
                            .Sum(file => file.Size);
        PlanText.Text = $"{Formatting.Plural(doomed.Count, "file")} · {Formatting.Bytes(freed)}";
        AcceptButton.IsEnabled = doomed.Count > 0;
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
