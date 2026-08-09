using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class OcrNotice : Window
{
    private OcrNotice(IReadOnlyList<string> languages)
    {
        InitializeComponent();
        Title = $"{Branding.Name} — Text recognition";
        LanguagesText.Text = languages.Count > 0
            ? string.Join("   ", languages)
            : "No recognizer is installed. Text recognition will not work until you add one.";
    }

    internal static void ShowOnce(IReadOnlyList<string> languages)
    {
        var settings = Settings.Load();
        if (settings.SuppressOcrLanguageNotice) return;

        var dialog = new OcrNotice(languages);
        bool accepted = dialog.ShowDialog() == true;

        if (!accepted || !dialog.SuppressBox.IsChecked.GetValueOrDefault()) return;
        settings.SuppressOcrLanguageNotice = true;
        settings.Save();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
