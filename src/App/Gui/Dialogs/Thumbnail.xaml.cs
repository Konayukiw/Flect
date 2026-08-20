using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Optimizer.Gui.Dialogs;

public partial class Thumbnail : Window
{
    private readonly double _duration;
    private readonly DispatcherTimer _ticker;

    private bool _ready;
    private bool _playing;
    private bool _seeking;

    private Thumbnail(string path, double durationSeconds)
    {
        InitializeComponent();

        _duration = durationSeconds;
        Title = $"{Branding.Name} — {Loc.T("dialog.thumbnail.title")}";
        HintText.Text = Loc.T("dialog.thumbnail.hint");

        Scrubber.Maximum = _duration;
        DurationText.Text = Formatting.Timecode(_duration);

        TimeBox.Text = Formatting.Timecode(0);

        _ticker = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _ticker.Tick += OnTick;

        try
        {
            Player.Source = new Uri(path);
            Player.Position = TimeSpan.Zero;
            Player.Play();
            Player.Pause();
        }
        catch (UriFormatException)
        {
            HidePreview();
        }

        _ready = true;
        ShowPosition(0);
        ShowSelection();
    }

    internal static double? Ask(string path, double durationSeconds)
    {
        if (durationSeconds <= 0) return null;

        var dialog = new Thumbnail(path, durationSeconds);
        if (dialog.ShowDialog() != true) return null;

        if (!Formatting.TryParseTimecode(dialog.TimeBox.Text, out var seconds)) return null;
        return seconds;
    }

    private void HidePreview()
    {
        PreviewPanel.Visibility = Visibility.Collapsed;
        PlayButton.IsEnabled = false;
        _playing = false;
        _ticker.Stop();
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e) => HidePreview();

    private void OnMediaEnded(object sender, RoutedEventArgs e) => Pause();

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_playing) return;

        double position = Player.Position.TotalSeconds;

        _seeking = true;
        Scrubber.Value = Math.Clamp(position, 0, _duration);
        _seeking = false;
        ShowPosition(position);
    }

    private void OnScrubberChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _seeking) return;

        if (PreviewPanel.Visibility == Visibility.Visible)
        {
            Player.Position = TimeSpan.FromSeconds(e.NewValue);
        }
        ShowPosition(e.NewValue);
    }

    private void OnPlayPause(object sender, RoutedEventArgs e)
    {
        if (_playing)
        {
            Pause();
            return;
        }

        if (Formatting.TryParseTimecode(TimeBox.Text, out var time) && Scrubber.Value >= time)
        {
            Scrubber.Value = time;
        }

        Player.Play();
        _playing = true;
        PlayButton.Content = Loc.T("dialog.trim.pause");
        _ticker.Start();
    }

    private void Pause()
    {
        Player.Pause();
        _playing = false;
        PlayButton.Content = Loc.T("dialog.trim.play");
        _ticker.Stop();
    }

    private void OnSetTime(object sender, RoutedEventArgs e)
    {
        TimeBox.Text = Formatting.Timecode(Scrubber.Value);
    }

    private void OnTimeChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        ShowSelection();
    }

    private void ShowPosition(double seconds) =>
        PositionText.Text = Formatting.Timecode(seconds);

    private void ShowSelection()
    {
        SelectionText.Text = Formatting.TryParseTimecode(TimeBox.Text, out var seconds)
            ? Loc.F("dialog.thumbnail.selection", Formatting.Timecode(seconds))
            : string.Empty;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (!Formatting.TryParseTimecode(TimeBox.Text, out var time))
        {
            Report.Error(Loc.T("dialog.thumbnail.invalid"));
            return;
        }
        if (time > _duration)
        {
            Report.Error(Loc.T("dialog.thumbnail.past"));
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnClosed(object? sender, EventArgs e)
    {
        _ticker.Stop();
        _ticker.Tick -= OnTick;
        Player.Stop();
        Player.Close();
        Player.Source = null;
    }
}