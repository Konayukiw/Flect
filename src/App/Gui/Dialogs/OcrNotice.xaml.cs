using System.Windows;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class OcrNotice : Window
{
    private OcrNotice(IReadOnlyList<string> languages)
    {
        InitializeComponent();
        Title = $"{Branding.Name} — {Loc.T("dialog.ocr.title")}";
        LanguagesText.Text = languages.Count > 0
            ? string.Join("   ", languages)
            : Loc.T("dialog.ocr.none");
    }

    internal static void ShowOnce(IReadOnlyList<string> languages)
    {
        var settings = Settings.Current;
        if (settings.Ocr.SuppressLanguageNotice) return;

        var dialog = new OcrNotice(languages);
        bool accepted = dialog.ShowDialog() == true;

        if (!accepted || !dialog.SuppressBox.IsChecked.GetValueOrDefault()) return;

        settings.Ocr.SuppressLanguageNotice = true;
        settings.Save();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
