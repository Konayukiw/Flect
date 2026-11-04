using System.Windows;

namespace Flect.Ocr;

public partial class NoticeWindow : Window
{
    private NoticeWindow(IReadOnlyList<string> languages)
    {
        InitializeComponent();
        Title = $"{Branding.Name} - {Loc.T("dialog.ocr.title")}";
        LanguagesText.Text = languages.Count > 0
            ? string.Join("   ", languages)
            : Loc.T("dialog.ocr.none");
    }

    internal static void ShowOnce(IReadOnlyList<string> languages)
    {
        var settings = Preferences.Current;
        if (settings.Ocr.SuppressLanguageNotice) return;

        var dialog = new NoticeWindow(languages);
        bool accepted = dialog.ShowDialog() == true;

        if (!accepted || !dialog.SuppressBox.IsChecked.GetValueOrDefault()) return;

        settings.Ocr.SuppressLanguageNotice = true;
        settings.Save();
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
