using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

internal sealed record PixelTarget(uint Width, uint Height, bool KeepAspect);

public partial class PixelsDialog : Window
{
    private PixelsDialog()
    {
        InitializeComponent();
        Title = $"{Branding.Name} — Resize";
        WidthBox.Focus();
    }

    internal static PixelTarget? Ask()
    {
        var dialog = new PixelsDialog();
        if (dialog.ShowDialog() != true) return null;

        return new PixelTarget(Read(dialog.WidthBox.Text), Read(dialog.HeightBox.Text),
                               dialog.KeepAspectBox.IsChecked == true);
    }

    private static uint Read(string text) =>
        uint.TryParse(text.Trim(), out var value) ? value : 0;

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        uint width = Read(WidthBox.Text);
        uint height = Read(HeightBox.Text);

        if (width == 0 && height == 0)
        {
            Report.Error("Enter a width, a height, or both.");
            return;
        }
        if (width > 100000 || height > 100000)
        {
            Report.Error("That size is not sensible.");
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
