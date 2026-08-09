using System.Windows;
using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

internal sealed record RenamePlan(string Prefix, string Name, string Suffix, bool Numbered);

public partial class RenameDialog : Window
{
    private readonly string _sampleExtension;

    private bool _ready;

    private RenameDialog(string sampleExtension)
    {
        _sampleExtension = sampleExtension;
        InitializeComponent();
        Title = $"{Branding.Name} — Rename";
        _ready = true;
        ApplyNumberingState();
        PrefixBox.Focus();
    }

    internal static RenamePlan? Ask(string sampleExtension)
    {
        var dialog = new RenameDialog(sampleExtension);
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
        var middle = Numbering ? "001" : NameBox.Text;
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
            Report.Error(Numbering
                ? "Give at least a prefix or a suffix."
                : "Give a custom name, or turn numbering on.");
            return;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Report.Error("A name cannot contain \\ / : * ? \" < > |");
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
