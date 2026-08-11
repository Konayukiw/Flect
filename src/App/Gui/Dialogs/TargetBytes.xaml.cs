using System.Windows;
using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class TargetBytes : Window
{
    private bool _ready;

    private TargetBytes(long largestSelected)
    {
        InitializeComponent();
        _ready = true;
        Title = $"{Branding.Name} — {Loc.T("dialog.target.title")}";

        var syntax = Loc.T("dialog.target.syntax");
        HintText.Text = largestSelected > 0
            ? Loc.F("dialog.target.largest", Formatting.Bytes(largestSelected), syntax)
            : syntax;

        SizeBox.Focus();
    }

    internal static long? Ask(long largestSelected)
    {
        var dialog = new TargetBytes(largestSelected);
        if (dialog.ShowDialog() != true) return null;
        return Formatting.TryParseBytes(dialog.SizeBox.Text, out var bytes) ? bytes : null;
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        EchoText.Text = Formatting.TryParseBytes(SizeBox.Text, out var bytes)
            ? Loc.F("dialog.target.echo", Formatting.Count(bytes))
            : string.Empty;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (!Formatting.TryParseBytes(SizeBox.Text, out _))
        {
            Report.Error(Loc.T("dialog.target.invalid"));
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
