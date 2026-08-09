using System.Windows;
using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class TargetBytesDialog : Window
{
    private bool _ready;

    private TargetBytesDialog(long largestSelected)
    {
        InitializeComponent();
        _ready = true;
        Title = $"{Branding.Name} — Compress";
        HintText.Text = largestSelected > 0
            ? $"Largest selected file is {Formatting.Bytes(largestSelected)}. " +
              "Accepts 8MB, 900KB or a plain byte count."
            : "Accepts 8MB, 900KB or a plain byte count.";
        SizeBox.Focus();
    }

    internal static long? Ask(long largestSelected)
    {
        var dialog = new TargetBytesDialog(largestSelected);
        if (dialog.ShowDialog() != true) return null;
        return Formatting.TryParseBytes(dialog.SizeBox.Text, out var bytes) ? bytes : null;
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        EchoText.Text = Formatting.TryParseBytes(SizeBox.Text, out var bytes)
            ? $"= {Formatting.Count(bytes)} bytes"
            : string.Empty;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (!Formatting.TryParseBytes(SizeBox.Text, out _))
        {
            Report.Error("Enter a size such as 8MB, 900KB or 1500000.");
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
