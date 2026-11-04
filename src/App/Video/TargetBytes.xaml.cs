using System.Windows;
using System.Windows.Controls;

namespace Flect.Video;

public partial class TargetBytes : Window
{
    private bool _ready;

    private TargetBytes(long largestSelected)
    {
        InitializeComponent();
        _ready = true;
        Title = $"{Branding.Name} - {Loc.T("dialog.target.title")}";

        var syntax = Loc.T("dialog.target.syntax");
        HintText.Text = largestSelected > 0
            ? Loc.F("dialog.target.largest", Flect.Display.Format.Bytes(largestSelected), syntax)
            : syntax;

        SizeBox.Focus();
    }

    internal static long? Ask(long largestSelected)
    {
        var dialog = new TargetBytes(largestSelected);
        if (dialog.ShowDialog() != true) return null;
        return Flect.Display.Format.TryParseBytes(dialog.SizeBox.Text, out var bytes) ? bytes : null;
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        EchoText.Text = Flect.Display.Format.TryParseBytes(SizeBox.Text, out var bytes)
            ? Loc.F("dialog.target.echo", Flect.Display.Format.Count(bytes))
            : string.Empty;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (!Flect.Display.Format.TryParseBytes(SizeBox.Text, out _))
        {
            Flect.Display.Notice.Error(Loc.T("dialog.target.invalid"));
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
