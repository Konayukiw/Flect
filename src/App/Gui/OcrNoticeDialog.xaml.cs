using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class OcrNoticeDialog : Window
{
    private OcrNoticeDialog(IReadOnlyList<string> languages)
    {
        InitializeComponent();
        Title = $"{Branding.Name} — Text recognition";
        LanguagesText.Text = languages.Count > 0
            ? string.Join("   ", languages)
            : "No recognizer is installed. Text recognition will not work until you add one.";
    }

    internal static void ShowOnce(IReadOnlyList<string> languages)
    {
        var settings = UserSettings.Load();
        if (settings.SuppressOcrLanguageNotice) return;

        var dialog = new OcrNoticeDialog(languages);
        bool accepted = dialog.ShowDialog() == true;

        if (!accepted || !dialog.SuppressBox.IsChecked.GetValueOrDefault()) return;
        settings.SuppressOcrLanguageNotice = true;
        settings.Save();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
