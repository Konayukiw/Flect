using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Optimizer.Gui.Dialogs;

internal sealed record TrimRange(double StartSeconds, double EndSeconds)
{
    public double DurationSeconds => EndSeconds - StartSeconds;
}

public partial class Trim : Window
{
    private readonly double _duration;
    private readonly DispatcherTimer _ticker;

    private readonly ScrubDrag _scrub = new();

    private bool _ready;
    private bool _playing;
    private bool _seeking;

    private Trim(string path, double durationSeconds, bool picture)
    {
        InitializeComponent();

        _duration = durationSeconds;
        if (!picture) HidePicture();
        Title = $"{Branding.Name} - {Loc.T("dialog.trim.title")}";
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
        HookScrubber();
    }

    internal static TrimRange? Ask(string path, double durationSeconds, bool picture = true)
    {
        if (durationSeconds <= 0) return null;

        var dialog = new Trim(path, durationSeconds, picture);
        if (dialog.ShowDialog() != true) return null;

        return dialog.Range();
    }

    private TrimRange? Range()
    {
        if (!Formatting.TryParseTimecode(StartBox.Text, out var start)) return null;
        if (!Formatting.TryParseTimecode(EndBox.Text, out var end)) return null;
        return new TrimRange(start, end);
    }

    private void HidePicture()
    {
        Player.Height = 0;
        Player.IsHitTestVisible = false;
        PreviewPanel.BorderThickness = new Thickness(0);
        PreviewPanel.Padding = new Thickness(0);
        PreviewPanel.Margin = new Thickness(0);
        PreviewPanel.Background = Brushes.Transparent;
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

    // A track click jumps the pointer and then marks the click handled, so the
    // thumb never receives the drag. Follow the pointer until it is released.
    private void HookScrubber()
    {
        Scrubber.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnScrubberPress), handledEventsToo: true);
        Scrubber.AddHandler(UIElement.PreviewMouseMoveEvent,
            new MouseEventHandler(OnScrubberMove), handledEventsToo: true);
        Scrubber.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnScrubberRelease), handledEventsToo: true);
        Scrubber.LostMouseCapture += OnScrubberLostCapture;
    }

    private void OnScrubberPress(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !TrySeconds(e, out var seconds)) return;

        var target = PointerHit(e) ? ScrubTarget.Thumb : ScrubTarget.Track;
        if (_scrub.Begin(target, seconds) is not double placed) return;

        e.Handled = true;
        if (_playing) Pause();
        Place(placed);
        Scrubber.CaptureMouse();
    }

    private void OnScrubberMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !_scrub.Active) return;
        if (!TrySeconds(e, out var seconds)) return;
        if (_scrub.Drag(seconds) is double placed) Place(placed);
    }

    private void OnScrubberRelease(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        FinishTrackDrag(e);
    }

    private void OnScrubberLostCapture(object sender, MouseEventArgs e) => FinishTrackDrag(e);

    private void FinishTrackDrag(MouseEventArgs e)
    {
        if (!_scrub.Active) return;

        if (!TrySeconds(e, out var seconds)) _scrub.Cancel();
        else if (_scrub.End(seconds) is double placed) Place(placed);

        if (Scrubber.IsMouseCaptured) Scrubber.ReleaseMouseCapture();
    }

    private void Place(double seconds) => Scrubber.Value = seconds;

    private static bool PointerHit(MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null)
        {
            if (source is Thumb) return true;
            if (source is not Visual) return false;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private Track? TrackOf() => Scrubber.Template?.FindName("PART_Track", Scrubber) as Track;

    private bool TrySeconds(MouseEventArgs e, out double seconds)
    {
        seconds = 0;
        var track = TrackOf();
        if (track is null) return false;

        var value = track.ValueFromPoint(e.GetPosition(track));
        if (!double.IsFinite(value)) return false;

        seconds = Math.Clamp(value, Scrubber.Minimum, Scrubber.Maximum);
        return true;
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
