using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Flect.Settings.Output;

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
