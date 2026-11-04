using System.Windows;
using System.Windows.Controls;

namespace Flect.Ocr;

public partial class OcrPage : UserControl, ISettings
{
    public OcrPage()
    {
        InitializeComponent();

        Flect.Settings.Binding.Fill(EncodingBox,
            (TextEncodingChoice.Utf8, "settings.ocr.encoding.utf8"),
            (TextEncodingChoice.Utf8Bom, "settings.ocr.encoding.utf8bom"),
            (TextEncodingChoice.Utf16, "settings.ocr.encoding.utf16"),
            (TextEncodingChoice.ShiftJis, "settings.ocr.encoding.sjis"));

        Flect.Settings.Binding.FillText(InstallBox, OcrLanguages.Installable
            .Select(tag => (tag, $"{OcrLanguages.Describe(tag)} ({tag})")));
        InstallBox.SelectedIndex = 0;
    }

    void ISettings.Load(Preferences settings)
    {
        var ocr = settings.Ocr;

        LoadRecognizers(ocr.Language);

        OutputFileBox.IsChecked = ocr.Output == OcrOutput.TextFile;
        OutputClipboardBox.IsChecked = ocr.Output == OcrOutput.Clipboard;
        OutputShowBox.IsChecked = ocr.Output == OcrOutput.ShowOnly;
        UpdateEncodingState();

        Flect.Settings.Binding.Select(EncodingBox, ocr.Encoding);
        SuppressNoticeBox.IsChecked = ocr.SuppressLanguageNotice;
    }

    void ISettings.Store(Preferences settings)
    {
        var ocr = settings.Ocr;

        ocr.Language = LanguageBox.SelectedItem is Choice { Value: string tag } ? tag : string.Empty;

        ocr.Output = OutputClipboardBox.IsChecked == true ? OcrOutput.Clipboard
                   : OutputShowBox.IsChecked == true ? OcrOutput.ShowOnly
                   : OcrOutput.TextFile;

        ocr.Encoding = Flect.Settings.Binding.Selected(EncodingBox, ocr.Encoding);
        ocr.SuppressLanguageNotice = SuppressNoticeBox.IsChecked == true;
    }

    private void LoadRecognizers(string selected)
    {
        var installed = OcrLanguages.Installed();

        var choices = new List<(string, string)>
        {
            (string.Empty, Loc.T("settings.ocr.language.system")),
        };
        choices.AddRange(installed.Select(language => (language.Tag, language.Display)));

        Flect.Settings.Binding.FillText(LanguageBox, choices);
        Flect.Settings.Binding.Select(LanguageBox, selected);

        InstalledText.Text = installed.Count == 0
            ? Loc.T("settings.ocr.language.none")
            : Loc.T("settings.ocr.language.installed") + ": " +
              string.Join(" · ", installed.Select(language => language.Display));
    }

    private void OnOutputChanged(object sender, RoutedEventArgs e) => UpdateEncodingState();

    private void UpdateEncodingState()
    {
        if (EncodingBox is null) return;

        bool writesFile = OutputFileBox.IsChecked == true;
        EncodingBox.IsEnabled = writesFile;
        EncodingLabel.Opacity = writesFile ? 1 : 0.45;
    }

    private void OnInstall(object sender, RoutedEventArgs e)
    {
        if (InstallBox.SelectedItem is not Choice { Value: string tag }) return;

        var answer = MessageBox.Show(Window.GetWindow(this)!,
                                     Loc.T("settings.ocr.language.installWarning"), Branding.Name,
                                     MessageBoxButton.OKCancel, MessageBoxImage.Warning,
                                     MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;

        var key = OcrLanguages.StartInstaller(tag)
            ? "settings.ocr.language.installStarted"
            : "settings.ocr.language.installFailed";

        MessageBox.Show(Window.GetWindow(this)!, Loc.T(key), Branding.Name, MessageBoxButton.OK,
                        MessageBoxImage.Information);
    }
}
