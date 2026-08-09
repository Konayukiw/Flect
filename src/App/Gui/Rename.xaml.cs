using System.Windows;
using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

internal sealed record RenamePlan(string Prefix, string Name, string Suffix, bool Numbered);

public partial class Rename : Window
{
    private readonly string _sampleExtension;
    private readonly int _digits;
    private readonly int _start;

    private bool _ready;

    private Rename(string sampleExtension)
    {
        _sampleExtension = sampleExtension;

        var rename = Settings.Current.Folder.Rename;
        _digits = rename.DigitCount;
        _start = rename.StartNumber;

        InitializeComponent();
        Title = $"{Branding.Name} — {Loc.T("dialog.rename.title")}";
        NumberBox.IsChecked = rename.UseNumbering;

        _ready = true;
        ApplyNumberingState();
        PrefixBox.Focus();
    }

    internal static RenamePlan? Ask(string sampleExtension)
    {
        var dialog = new Rename(sampleExtension);
        if (dialog.ShowDialog() != true) return null;

        return new RenamePlan(dialog.PrefixBox.Text, dialog.NameBox.Text, dialog.SuffixBox.Text,
                              dialog.NumberBox.IsChecked.GetValueOrDefault());
    }

    private bool Numbering => NumberBox.IsChecked.GetValueOrDefault();

    private void OnInputChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void OnNumberingChanged(object sender, RoutedEventArgs e) => ApplyNumberingState();

    private void ApplyNumberingState()
    {
        if (!_ready) return;
        NameBox.IsEnabled = !Numbering;
        NameLabel.IsEnabled = !Numbering;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (!_ready) return;

        var middle = Numbering
            ? _start.ToString().PadLeft(_digits > 0 ? _digits : 3, '0')
            : NameBox.Text;
        var first = $"{PrefixBox.Text}{middle}{SuffixBox.Text}{_sampleExtension}";

        PreviewText.Text = Numbering
            ? first
            : $"{first}   {PrefixBox.Text}{middle}{SuffixBox.Text} (2){_sampleExtension}";
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var name = PrefixBox.Text + (Numbering ? "1" : NameBox.Text) + SuffixBox.Text;
        if (name.Trim().Length == 0)
        {
            Report.Error(Loc.T(Numbering ? "dialog.rename.needAffix" : "dialog.rename.needName"));
            return;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Report.Error(Loc.T("dialog.rename.invalidChars"));
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
