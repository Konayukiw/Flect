using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class TextResult : Window
{
    public TextResult(string heading, string text)
    {
        InitializeComponent();
        Title = $"{Branding.Name} — {heading}";
        HeadingText.Text = heading;
        Body.Text = text;
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Body.Text);
            CopyButton.Content = Loc.T("common.copied");
        }
        catch (Exception)
        {
            CopyButton.Content = Loc.T("common.copyFailed");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
