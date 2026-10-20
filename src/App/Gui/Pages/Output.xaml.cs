using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

namespace Optimizer.Gui.Pages;

public sealed class OutputRow : INotifyPropertyChanged
{
    private string _path;

    public OutputRow(string key, string label, string path)
    {
        Key = key;
        Label = label;
        _path = path;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Label { get; }

    public string Path
    {
        get => _path;
        set
        {
            if (_path == value) return;
            _path = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Path)));
        }
    }
}

public partial class Output : UserControl, ISettingsPage
{
    private readonly ObservableCollection<OutputRow> _rows = [];

    public Output()
    {
        InitializeComponent();
        RuleList.ItemsSource = _rows;
    }

    void ISettingsPage.Load(Settings settings)
    {
        _rows.Clear();

        var folders = settings.Output?.Folders;
        if (folders is not null)
        {
            foreach (var feature in OutputCatalog.Features)
            {
                if (folders.TryGetValue(feature.Key, out var path) &&
                    !string.IsNullOrWhiteSpace(path))
                {
                    _rows.Add(new OutputRow(feature.Key, LabelFor(feature), path));
                }
            }
        }

        RefreshFeatures();
    }

    void ISettingsPage.Store(Settings settings)
    {
        var folders = settings.Output?.Folders;
        if (folders is null) return;

        foreach (var feature in OutputCatalog.Features) folders.Remove(feature.Key);

        var empty = new List<OutputRow>();
        foreach (var row in _rows)
        {
            var path = row.Path.Trim();
            if (path.Length == 0)
            {
                empty.Add(row);
                continue;
            }
            folders[row.Key] = path;
        }

        if (empty.Count == 0) return;

        foreach (var row in empty) _rows.Remove(row);
        RefreshFeatures();
    }

    private static string LabelFor(OutputFeature feature) =>
        $"{Loc.T(feature.CategoryKey)} / {Loc.T(feature.LabelKey)}";

    private void RefreshFeatures()
    {
        var used = _rows.Select(row => row.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = OutputCatalog.Features
            .Where(feature => !used.Contains(feature.Key))
            .Select(feature => (Value: feature.Key, Label: LabelFor(feature)))
            .ToList();

        Fields.FillText(FeatureBox, items);
        if (items.Count > 0) FeatureBox.SelectedIndex = 0;
        FeatureBox.IsEnabled = items.Count > 0;
        AddButton.IsEnabled = items.Count > 0;
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var key = Fields.Selected(FeatureBox, string.Empty);
        if (key.Length == 0) return;

        var feature = OutputCatalog.Features.First(item => item.Key == key);
        _rows.Add(new OutputRow(feature.Key, LabelFor(feature), string.Empty));
        RefreshFeatures();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not OutputRow row) return;

        var dialog = new OpenFolderDialog();
        var current = row.Path.Trim();
        if (current.Length > 0)
        {
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(current);
                if (Path.IsPathRooted(expanded) && Directory.Exists(expanded))
                {
                    dialog.InitialDirectory = expanded;
                }
            }
            catch (Exception) {}
        }

        if (dialog.ShowDialog(Window.GetWindow(this)) == true) row.Path = dialog.FolderName;
    }

    private void OnDefault(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not OutputRow row) return;

        _rows.Remove(row);
        RefreshFeatures();
    }
}
