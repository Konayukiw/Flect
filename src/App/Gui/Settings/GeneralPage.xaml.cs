using System.Windows;
using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class GeneralPage : UserControl, ISettingsPage
{
    private List<MenuOption> _menu = [];

    public GeneralPage()
    {
        InitializeComponent();

        Fields.Fill(ThemeBox,
            (AppTheme.System, "settings.general.theme.system"),
            (AppTheme.Light, "settings.general.theme.light"),
            (AppTheme.Dark, "settings.general.theme.dark"));

        Fields.Fill(LanguageBox,
            (AppLanguage.System, "settings.general.language.system"),
            (AppLanguage.English, "settings.general.language.en"),
            (AppLanguage.Japanese, "settings.general.language.ja"),
            (AppLanguage.ChineseSimplified, "settings.general.language.zh"));
    }

    void ISettingsPage.Load(Settings settings)
    {
        var general = settings.General;

        Fields.Select(ThemeBox, general.Theme);
        Fields.Select(LanguageBox, general.Language);

        CloseOnSuccessBox.IsChecked = general.CloseOnSuccess;
        CloseOnFailureBox.IsChecked = general.CloseOnFailure;
        CloseOnCancelBox.IsChecked = general.CloseOnCancel;

        var hidden = new HashSet<string>(general.HiddenMenuItems, StringComparer.OrdinalIgnoreCase);
        _menu = MenuOption.Build(MenuCatalog.Categories, hidden);

        foreach (var option in _menu) option.IsExpanded = ShowsHidden(option);

        MenuTree.ItemsSource = _menu;
    }

    void ISettingsPage.Store(Settings settings)
    {
        var general = settings.General;

        general.Theme = Fields.Selected(ThemeBox, general.Theme);
        general.Language = Fields.Selected(LanguageBox, general.Language);

        general.CloseOnSuccess = CloseOnSuccessBox.IsChecked == true;
        general.CloseOnFailure = CloseOnFailureBox.IsChecked == true;
        general.CloseOnCancel = CloseOnCancelBox.IsChecked == true;

        var hidden = new List<string>();
        MenuOption.CollectHidden(_menu, hidden);
        general.HiddenMenuItems = hidden;
    }

    private static bool ShowsHidden(MenuOption option) =>
        option.IsChecked != true || option.Children.Any(ShowsHidden);

    private void OnCheckAll(object sender, RoutedEventArgs e) => MenuOption.SetAll(_menu, true);

    private void OnCheckNone(object sender, RoutedEventArgs e) => MenuOption.SetAll(_menu, false);
}
