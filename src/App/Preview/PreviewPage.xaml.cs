using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Flect.ExplorerPreview;

public partial class PreviewPage : UserControl, ISettings
{
    private const int MinimumFontSize = 6;
    private const int MaximumFontSize = 72;

    private static readonly (string Kind, string Key)[] Kinds =
    [
        ("text", "settings.preview.kind.text"),
        ("html", "settings.preview.kind.html"),
        ("markdown", "settings.preview.kind.markdown"),
        ("svg", "settings.preview.kind.svg"),
        ("pdf", "settings.preview.kind.pdf"),
        ("font", "settings.preview.kind.font"),
        ("video", "settings.preview.kind.video"),
        ("audio", "settings.preview.kind.audio"),
    ];

    private readonly List<CheckBox> _kindBoxes = [];
    private readonly List<CheckBox> _thirdPartyBoxes = [];

    public PreviewPage()
    {
        InitializeComponent();

        var fonts = Fonts.SystemFontFamilies
                         .Select(family => family.Source)
                         .Where(name => !string.Equals(name, PreviewSettings.DefaultFont, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                         .Select(name => (name, name))
                         .Prepend((PreviewSettings.DefaultFont, PreviewSettings.DefaultFont));
        Binding.FillText(FontBox, fonts);

        foreach (var (kind, key) in Kinds)
        {
            var box = new CheckBox { Content = Loc.T(key), Tag = kind };
            _kindBoxes.Add(box);
            KindList.Items.Add(box);
        }
    }

    void ISettings.Load(Preferences settings)
    {
        var preview = settings.Preview;

        EnabledBox.IsChecked = preview.Enabled;
        Binding.Select(FontBox, preview.FontFamily);
        Binding.ShowInt(FontSizeBox, preview.FontSize);
        WordWrapBox.IsChecked = preview.WordWrap;
        SampleBox.Text = preview.SampleText;
        TrustedBox.Text = string.Join(Environment.NewLine, preview.TrustedFolders);

        var disabled = new HashSet<string>(preview.DisabledKinds, StringComparer.OrdinalIgnoreCase);
        foreach (var box in _kindBoxes) box.IsChecked = !disabled.Contains((string)box.Tag);

        LoadThirdParty(preview);
    }

    void ISettings.Store(Preferences settings)
    {
        var preview = settings.Preview;
        var associationKey = AssociationKey(preview);

        preview.Enabled = EnabledBox.IsChecked == true;
        preview.FontFamily = Binding.Selected(FontBox, preview.FontFamily);
        preview.FontSize = Binding.Int(FontSizeBox, preview.FontSize, MinimumFontSize, MaximumFontSize);
        preview.WordWrap = WordWrapBox.IsChecked == true;
        preview.SampleText = SampleBox.Text;
        preview.TrustedFolders = ReadFolders(TrustedBox.Text);
        preview.DisabledKinds = _kindBoxes.Where(box => box.IsChecked != true).Select(box => (string)box.Tag).ToList();
        preview.ThirdPartyOverrides = _thirdPartyBoxes.Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToList();

        if (AssociationKey(preview) == associationKey) return;

        PreviewAssociations.Apply(preview);
        LoadThirdParty(preview);
    }

    private static string AssociationKey(PreviewSettings preview) =>
        string.Join('|', preview.Enabled, string.Join(';', preview.DisabledKinds), string.Join(';', preview.ThirdPartyOverrides));

    private static List<string> ReadFolders(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Path.IsPathFullyQualified)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void LoadThirdParty(PreviewSettings preview)
    {
        var replaced = new HashSet<string>(preview.ThirdPartyOverrides, StringComparer.OrdinalIgnoreCase);
        _thirdPartyBoxes.Clear();
        ThirdPartyList.Items.Clear();

        foreach (var handler in PreviewAssociations.FindThirdParty(preview))
        {
            var box = new CheckBox
            {
                Content = Loc.F("settings.preview.thirdParty.item", handler.Extension, handler.Handler),
                Tag = handler.Extension,
                IsChecked = replaced.Contains(handler.Extension),
            };
            _thirdPartyBoxes.Add(box);
            ThirdPartyList.Items.Add(box);
        }

        ThirdPartySection.Visibility = _thirdPartyBoxes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBrowseTrusted(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(Window.GetWindow(this)) is not true) return;

        var current = TrustedBox.Text.TrimEnd();
        TrustedBox.Text = current.Length == 0 ? dialog.FolderName : current + Environment.NewLine + dialog.FolderName;
    }
}
