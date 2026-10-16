using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Optimizer.Gui.Dialogs;

public sealed record PdfOrderRow(string Position, string Name, string FullPath);

public partial class PdfOrder : Window
{
    private readonly List<string> _paths;
    private readonly ObservableCollection<PdfOrderRow> _rows = [];

    private PdfOrder(IEnumerable<string> paths)
    {
        _paths = [.. paths];
        InitializeComponent();
        Title = $"{Branding.Name} - {Loc.T("dialog.pdfOrder.title")}";
        RowList.ItemsSource = _rows;
        Refresh(0);
    }

    internal static IReadOnlyList<string>? Ask(IEnumerable<string> paths)
    {
        var dialog = new PdfOrder(paths);
        return dialog.ShowDialog() == true ? dialog._paths : null;
    }

    private void Refresh(int selected)
    {
        _rows.Clear();
        for (int index = 0; index < _paths.Count; index++)
        {
            _rows.Add(new PdfOrderRow((index + 1).ToString(CultureInfo.CurrentCulture),
                                      Path.GetFileName(_paths[index]), _paths[index]));
        }

        RowList.SelectedIndex = selected;
        if (RowList.SelectedItem is not null) RowList.ScrollIntoView(RowList.SelectedItem);
        UpdateButtons();
    }

    private void Move(int offset)
    {
        int from = RowList.SelectedIndex;
        int to = from + offset;
        if (from < 0 || to < 0 || to >= _paths.Count) return;

        (_paths[from], _paths[to]) = (_paths[to], _paths[from]);
        Refresh(to);
    }

    private void UpdateButtons()
    {
        int index = RowList.SelectedIndex;
        UpButton.IsEnabled = index > 0;
        DownButton.IsEnabled = index >= 0 && index < _paths.Count - 1;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void OnUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnDown(object sender, RoutedEventArgs e) => Move(1);

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
