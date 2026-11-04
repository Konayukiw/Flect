using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Flect.Inspect.Window;

public partial class FileAnalyzeWindow : System.Windows.Window
{
    private readonly FileReport _report;
    private readonly Flect.Inspect.Suggest.Suggest _actions;
    private readonly CancellationTokenSource _hashCancel = new();
    private readonly Task<string> _hash;
    private readonly string _pathHint;
    private bool _hashReady;
    private DispatcherTimer? _copiedTimer;
    private FrameworkElement? _copiedHost;
    private ToolTip? _copiedTip;
    private object? _copiedRestore;
    private Point _copiedAt;
    private bool _watchCopiedMove;
    private Popup? _menu;

    internal FileAnalyzeWindow(FileReport report)
    {
        _report = report;
        _actions = new Flect.Inspect.Suggest.Suggest(report.Path, Length(report.Path), Preferences.Current.General,
                                      Preferences.Current.Image.Presets, Preferences.Current.Video.Presets);
        InitializeComponent();
        Title = $"{Branding.Name} - {Loc.T("dialog.fileAnalyze.title")}";
        _pathHint = report.Path + "\n" + Loc.T("dialog.fileAnalyze.pathHint");
        PathText.Text = report.Path;
        PathText.Cursor = Cursors.Hand;
        PathText.Focusable = true;
        PathText.ToolTip = _pathHint;
        AlertList.ItemsSource = report.Alerts;
        OverviewList.ItemsSource = Rows(report.Overview);
        SectionList.ItemsSource = report.Sections
            .Select(section => new AnalyzeSection(section.Title, Rows(section.Rows)))
            .ToList();
        HashText.Text = Loc.T("dialog.fileAnalyze.hashPending");
        _hash = Task.Run(() => Checksum.Sha256(report.Path, _hashCancel.Token));
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            HashText.Text = await _hash;
            _hashReady = true;
            HashText.Cursor = Cursors.Hand;
            HashText.Focusable = true;
            HashText.ToolTip = Loc.T("dialog.fileAnalyze.copyHint");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HashText.Text = ex.Message;
        }
    }

    private void OnValueClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not AnalyzeRow row || !row.CanCopy)
        {
            return;
        }
        Use(element, row.Key, row.Value, row.CopyHint, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void OnValueKey(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not AnalyzeRow row || !row.CanCopy)
        {
            return;
        }
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Copy(element, row.Value, row.CopyHint);
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Enter or Key.Space)) return;
        Use(element, row.Key, row.Value, row.CopyHint, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void OnPathClick(object sender, MouseButtonEventArgs e)
    {
        Use(PathText, "dialog.fileAnalyze.path", _report.Path, _pathHint, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void OnPathKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Copy(PathText, _report.Path, _pathHint);
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Enter or Key.Space)) return;
        Use(PathText, "dialog.fileAnalyze.path", _report.Path, _pathHint, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void OnHashClick(object sender, MouseButtonEventArgs e)
    {
        if (!_hashReady) return;
        Copy(HashText, HashText.Text, Loc.T("dialog.fileAnalyze.copyHint"));
        e.Handled = true;
    }

    private void OnHashKey(object sender, KeyEventArgs e)
    {
        if (!_hashReady) return;
        if ((e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) ||
            e.Key is Key.Enter or Key.Space)
        {
            Copy(HashText, HashText.Text, Loc.T("dialog.fileAnalyze.copyHint"));
            e.Handled = true;
        }
    }

    private void OnAction(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not AnalyzeRow row || !row.HasAction) return;
        CloseMenu();
        if (row.Direct)
        {
            Flect.Dispatch.Launch.Start(row.Commands[0].Verb, _report.Path);
            return;
        }

        var list = new StackPanel();
        var popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Custom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.None,
        };
        popup.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
        [
            new CustomPopupPlacement(
                new Point(targetSize.Width - popupSize.Width + 8, targetSize.Height + 3),
                PopupPrimaryAxis.Vertical),
            new CustomPopupPlacement(
                new Point(targetSize.Width - popupSize.Width + 8, -popupSize.Height - 3),
                PopupPrimaryAxis.Vertical),
        ];
        foreach (var command in row.Commands)
        {
            var verb = command.Verb;
            var item = new Button
            {
                Content = command.Label,
                Style = (Style)FindResource("FactMenuItem"),
            };
            item.Click += (_, _) =>
            {
                popup.IsOpen = false;
                Flect.Dispatch.Launch.Start(verb, _report.Path);
            };
            list.Children.Add(item);
        }

        popup.Child = new Border
        {
            Style = (Style)FindResource("FactMenu"),
            Child = new ScrollViewer
            {
                Content = list,
                MaxHeight = 320,
                Focusable = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            },
        };
        popup.PreviewKeyDown += (_, args) => OnMenuKey(popup, list, button, args);
        popup.Opened += (_, _) =>
        {
            if (list.Children.Count > 0) FocusMenuItem((FrameworkElement)list.Children[0]);
        };
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_menu, popup)) _menu = null;
        };
        _menu = popup;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ReferenceEquals(_menu, popup)) popup.IsOpen = true;
        }), DispatcherPriority.Input);
    }

    private void OnMenuKey(Popup popup, StackPanel list, Button anchor, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            popup.IsOpen = false;
            anchor.Focus();
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Down or Key.Up) || list.Children.Count == 0) return;

        var current = -1;
        for (var i = 0; i < list.Children.Count; i++)
        {
            if (list.Children[i] is UIElement element && element.IsKeyboardFocusWithin)
            {
                current = i;
                break;
            }
        }
        var count = list.Children.Count;
        var next = e.Key == Key.Down ? (current + 1) % count : (current - 1 + count) % count;
        FocusMenuItem((FrameworkElement)list.Children[next]);
        e.Handled = true;
    }

    private static void FocusMenuItem(FrameworkElement item)
    {
        item.Focus();
        item.BringIntoView();
    }

    private void CloseMenu()
    {
        if (_menu is null) return;
        _menu.IsOpen = false;
        _menu = null;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var format = Preferences.Current.Folder.Analyze.SaveFormat;
        var dialog = new SaveFileDialog
        {
            FileName = $"{Branding.Name}-file{AnalysisFile.Extension(format)}",
            DefaultExt = Flect.Folder.Analysis.AnalysisFile.Extension(format),
            Filter = Flect.Folder.Analysis.AnalysisFile.Filter(format),
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            string? hash = null;
            if (_hash.IsCompletedSuccessfully) hash = _hash.Result;
            else
            {
                HashText.Text = Loc.T("dialog.fileAnalyze.hashPending");
                _hashReady = false;
                HashText.Cursor = null;
                HashText.Focusable = false;
                hash = await _hash;
                HashText.Text = hash;
                _hashReady = true;
                HashText.Cursor = Cursors.Hand;
                HashText.Focusable = true;
                HashText.ToolTip = Loc.T("dialog.fileAnalyze.copyHint");
            }

            ReportFile.Write(_report, hash, dialog.FileName, format);
            Flect.Display.Notice.Info(Loc.F("dialog.analyze.saved", dialog.FileName));
        }
        catch (Exception ex)
        {
            Flect.Display.Notice.Error(Loc.F("dialog.analyze.saveFailed", ex.Message));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        HideCopied();
        CloseMenu();
        _hashCancel.Cancel();
        base.OnClosed(e);
    }

    private List<AnalyzeRow> Rows(IReadOnlyList<FileFact> facts)
    {
        var rows = new List<AnalyzeRow>(facts.Count);
        foreach (var fact in facts)
        {
            var link = _actions.Link(fact.Key);
            var copy = Flect.Inspect.Suggest.Suggest.CanCopy(fact.Key);
            rows.Add(new AnalyzeRow
            {
                Label = fact.Label,
                Value = fact.Value,
                Key = fact.Key,
                CanCopy = copy,
                CopyHint = fact.Key == "dialog.fileAnalyze.path"
                    ? Loc.T("dialog.fileAnalyze.pathHint")
                    : Loc.T("dialog.fileAnalyze.copyHint"),
                ActionLabel = link?.Label,
                Direct = link?.Direct == true,
                Commands = link?.Commands ?? [],
            });
        }
        return rows;
    }

    private void Use(FrameworkElement element, string? key, string value, object? restore,
                     ModifierKeys modifiers)
    {
        if (key == "dialog.fileAnalyze.path" &&
            (modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
        {
            Reveal(_report.Path);
            return;
        }
        Copy(element, value, restore);
    }

    private void Copy(FrameworkElement element, string value, object? restore)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            Clipboard.SetText(value);
            ShowCopied(element, restore);
        }
        catch (Exception)
        {
            Flect.Display.Notice.Error(Loc.T("common.copyFailed"));
        }
    }

    private void ShowCopied(FrameworkElement element, object? restore)
    {
        HideCopied();

        var tip = new ToolTip
        {
            Content = Loc.T("common.copied"),
            PlacementTarget = element,
            Placement = PlacementMode.Bottom,
        };
        element.ToolTip = tip;
        tip.IsOpen = true;
        _copiedHost = element;
        _copiedTip = tip;
        _copiedRestore = restore;
        _copiedAt = Mouse.GetPosition(this);
        PreviewMouseMove += OnCopiedMouseMove;
        _watchCopiedMove = true;
        _copiedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _copiedTimer.Tick += (_, _) => HideCopied();
        _copiedTimer.Start();
    }

    private void OnCopiedMouseMove(object sender, MouseEventArgs e)
    {
        if (e.GetPosition(this) == _copiedAt) return;
        HideCopied();
    }

    private void HideCopied()
    {
        if (_watchCopiedMove)
        {
            PreviewMouseMove -= OnCopiedMouseMove;
            _watchCopiedMove = false;
        }
        _copiedTimer?.Stop();
        if (_copiedTip is not null)
        {
            _copiedTip.IsOpen = false;
            _copiedTip = null;
        }
        if (_copiedHost is not null)
        {
            _copiedHost.ToolTip = _copiedRestore;
            _copiedHost = null;
        }
    }

    private static void Reveal(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Flect.Display.Notice.Error(ex.Message);
        }
    }

    private static long Length(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
