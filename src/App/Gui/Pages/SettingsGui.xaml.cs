using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

using Optimizer.Gui;

namespace Optimizer.Gui.Pages;

public partial class SettingsGui : Window
{
    private static readonly string[] Titles =
    [
        "settings.nav.general", "settings.nav.folder", "settings.nav.video",
        "settings.nav.image", "settings.nav.text", "settings.nav.ocr",
    ];

    private readonly UserControl[] _pages;

    private Settings _settings;
    private int _index;
    private bool _transitioning;

    private SettingsGui(int page)
    {
        InitializeComponent();
        Backdrop.Apply(this);

        _settings = Settings.Load();
        _pages = [PageGeneral, PageFolder, PageVideo, PageImage, PageText, PageOcr];

        Title = Loc.T("settings.title");
        SubtitleText.Text = Version();

        foreach (var control in _pages) ((ISettingsPage)control).Load(_settings);

        SelectPage(page);
    }

    public static void Open(int page = 0)
    {
        var existing = Application.Current.Windows.OfType<SettingsGui>().FirstOrDefault();
        if (existing is null)
        {
            new SettingsGui(page).Show();
            return;
        }

        existing.SelectPage(page);
        existing.Activate();
    }

    private static string Version()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? string.Empty : $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    private void SelectPage(int page)
    {
        _index = Math.Clamp(page, 0, _pages.Length - 1);

        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = i == _index ? Visibility.Visible : Visibility.Collapsed;
        }

        Tab(_index).IsChecked = true;
        PageTitle.Text = Loc.T(Titles[_index]);
        Scroller.ScrollToTop();
        AnimatePageIn();
    }

    private void AnimatePageIn()
    {
        if (_transitioning) return;

        _transitioning = true;
        var page = _pages[_index];

        page.RenderTransform = new System.Windows.Media.TranslateTransform(8, 0);
        page.Opacity = 0;

        var slide = new DoubleAnimation
        {
            From = 8,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        slide.Completed += (_, _) => _transitioning = false;
        page.RenderTransform.BeginAnimation(
            System.Windows.Media.TranslateTransform.XProperty, slide);

        page.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
            });
    }

    private RadioButton Tab(int page) => page switch
    {
        1 => NavFolder,
        2 => NavVideo,
        3 => NavImage,
        4 => NavText,
        5 => NavOcr,
        _ => NavGeneral,
    };

    private void OnNavChanged(object sender, RoutedEventArgs e)
    {
        if (_pages is null) return;

        if (sender is RadioButton { Tag: string tag } && int.TryParse(tag, out var page))
        {
            SelectPage(page);
        }
    }

    private bool Commit()
    {
        var previousLanguage = _settings.General.Language;
        var previousTheme = _settings.General.Theme;

        foreach (var control in _pages) ((ISettingsPage)control).Store(_settings);
        _settings.Save();

        if (_settings.General.Theme != previousTheme)
        {
            Theme.Apply(Application.Current, _settings.General.Theme);
        }

        if (_settings.General.Language == previousLanguage) return false;

        Loc.Use(_settings.General.Language);
        Replace();
        return true;
    }

    private SettingsGui Replace()
    {
        var replacement = new SettingsGui(_index);
        replacement.Show();
        Close();
        return replacement;
    }

    private void OnApply(object sender, RoutedEventArgs e) => Commit();

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (!Commit()) Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this, Loc.T("settings.reset.confirm"), Branding.Name,
                                     MessageBoxButton.OKCancel, MessageBoxImage.Warning,
                                     MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;

        Settings.Reset();
        _settings = Settings.Load();

        Loc.Use(_settings.General.Language);
        Theme.Apply(Application.Current, _settings.General.Theme);

        var replacement = Replace();
        MessageBox.Show(replacement, Loc.T("settings.reset.done"), Branding.Name,
                        MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}