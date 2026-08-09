using System.Windows;

namespace Optimizer.Gui;

public partial class TextResultWindow : Window
{
    public TextResultWindow(string heading, string text)
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
            CopyButton.Content = "Copied";
        }
        catch (Exception)
        {
            CopyButton.Content = "Copy failed";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
