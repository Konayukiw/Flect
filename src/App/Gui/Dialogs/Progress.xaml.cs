using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Optimizer.Gui.Dialogs;

public enum LogSeverity { Information, Warning, Error }

public sealed record LogEntry(LogSeverity Severity, string Message);

internal enum TaskOutcome { Succeeded, Cancelled, Failed }

public partial class Progress : Window, ITaskProgress
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ObservableCollection<LogEntry> _log = [];
    private bool _finished;
    private int _errorCount;
    private int _messageCount;

    public Progress(string title)
    {
        InitializeComponent();
        Title = $"{Branding.Name} — {title}";
        HeadingText.Text = title;
        ActionButton.Content = Loc.T("common.cancel");
        LogList.ItemsSource = _log;
    }

    public CancellationToken Token => _cancellation.Token;

    public void Status(string text) => Post(() => StatusText.Text = text);

    public void Step(int done, int total) => Post(() =>
    {
        Bar.IsIndeterminate = false;
        Bar.Maximum = Math.Max(1, total);
        Bar.Value = Math.Clamp(done, 0, Bar.Maximum);
    });

    public void Indeterminate() => Post(() => Bar.IsIndeterminate = true);

    public void Info(string message) => Append(LogSeverity.Information, message);
    public void Warn(string message) => Append(LogSeverity.Warning, message);

    public void Error(string message)
    {
        Interlocked.Increment(ref _errorCount);
        Append(LogSeverity.Error, message);
    }

    internal void Finish(TaskOutcome outcome, string? summary = null)
    {
        _finished = true;
        Bar.IsIndeterminate = false;
        ActionButton.Content = Loc.T("common.close");
        ActionButton.IsEnabled = true;

        StatusText.Text = summary ?? outcome switch
        {
            TaskOutcome.Succeeded when _errorCount > 0 =>
                Loc.F("progress.problems", Loc.N("count.problem", _errorCount)),
            TaskOutcome.Succeeded => Loc.T("progress.done"),
            TaskOutcome.Cancelled => Loc.T("progress.cancelled"),
            _ => Loc.T("progress.failed"),
        };

        if (ClosesItself(outcome)) Close();
    }

    private bool ClosesItself(TaskOutcome outcome)
    {
        var general = Settings.Current.General;

        return outcome switch
        {
            TaskOutcome.Succeeded => general.CloseOnSuccess && _errorCount == 0 && _messageCount == 0,
            TaskOutcome.Cancelled => general.CloseOnCancel,
            _ => general.CloseOnFailure,
        };
    }

    private void Append(LogSeverity severity, string message)
    {
        Interlocked.Increment(ref _messageCount);
        Post(() =>
        {
            _log.Add(new LogEntry(severity, message));
            LogPanel.Visibility = Visibility.Visible;
            LogScroller.ScrollToEnd();
        });
    }

    private void Post(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.InvokeAsync(action);
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (_finished)
        {
            Close();
            return;
        }
        ActionButton.IsEnabled = false;
        StatusText.Text = Loc.T("progress.cancelling");
        _cancellation.Cancel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_finished) _cancellation.Cancel();
        base.OnClosing(e);
    }
}
