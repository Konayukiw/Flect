using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Optimizer.Main;

namespace Optimizer.Gui;

internal sealed record TrimRange(double StartSeconds, double EndSeconds)
{
    public double DurationSeconds => EndSeconds - StartSeconds;
}

public partial class Trim : Window
{
    private readonly double _duration;
    private readonly DispatcherTimer _ticker;

    private bool _ready;
    private bool _playing;
    private bool _seeking;

    private Trim(string path, double durationSeconds)
    {
        InitializeComponent();

        _duration = durationSeconds;
        Title = $"{Branding.Name} — {Loc.T("dialog.trim.title")}";
        HintText.Text = Loc.T("dialog.trim.hint");

        Scrubber.Maximum = _duration;
        DurationText.Text = Formatting.Timecode(_duration);

        StartBox.Text = Formatting.Timecode(0);
        EndBox.Text = Formatting.Timecode(_duration);

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

    internal static TrimRange? Ask(string path, double durationSeconds)
    {
        if (durationSeconds <= 0) return null;

        var dialog = new Trim(path, durationSeconds);
        if (dialog.ShowDialog() != true) return null;

        return dialog.Range();
    }

    private TrimRange? Range()
    {
        if (!Formatting.TryParseTimecode(StartBox.Text, out var start)) return null;
        if (!Formatting.TryParseTimecode(EndBox.Text, out var end)) return null;
        return new TrimRange(start, end);
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

        if (Formatting.TryParseTimecode(EndBox.Text, out var end) && position >= end)
        {
            Pause();
            return;
        }

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

        if (Formatting.TryParseTimecode(EndBox.Text, out var end) &&
            Scrubber.Value >= end &&
            Formatting.TryParseTimecode(StartBox.Text, out var start))
        {
            Scrubber.Value = start;
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

    private void OnSetStart(object sender, RoutedEventArgs e)
    {
        StartBox.Text = Formatting.Timecode(Scrubber.Value);
    }

    private void OnSetEnd(object sender, RoutedEventArgs e)
    {
        EndBox.Text = Formatting.Timecode(Scrubber.Value);
    }

    private void OnTimesChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        ShowSelection();
    }

    private void ShowPosition(double seconds) =>
        PositionText.Text = Formatting.Timecode(seconds);

    private void ShowSelection()
    {
        var range = Range();
        SelectionText.Text = range is not null && range.DurationSeconds > 0
            ? Loc.F("dialog.trim.selection", Formatting.Timecode(range.DurationSeconds))
            : string.Empty;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var range = Range();
        if (range is null)
        {
            Report.Error(Loc.T("dialog.trim.invalid"));
            return;
        }
        if (range.StartSeconds >= range.EndSeconds)
        {
            Report.Error(Loc.T("dialog.trim.backwards"));
            return;
        }
        if (range.StartSeconds >= _duration)
        {
            Report.Error(Loc.T("dialog.trim.past"));
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
