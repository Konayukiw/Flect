using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Optimizer.Gui.Dialogs;

public partial class PdfPreviewWindow : Window
{
    private readonly string _path;
    private readonly int _pageCount;
    private int _page;

    public PdfPreviewWindow(string path)
    {
        InitializeComponent();
        _path = path;
        Title = $"{Branding.Name} — {Path.GetFileName(path)}";
        NameText.Text = Path.GetFileName(path);

        try
        {
            _pageCount = PdfRenderer.PageCount(path);
        }
        catch (Exception ex)
        {
            Report.Error($"Could not open this document.\n\n{ex.Message}");
            _pageCount = 0;
        }

        ShowPage(0);
    }

    private void ShowPage(int index)
    {
        if (_pageCount == 0)
        {
            PageText.Text = "—";
            PreviousButton.IsEnabled = NextButton.IsEnabled = false;
            return;
        }

        _page = Math.Clamp(index, 0, _pageCount - 1);
        PageText.Text = $"{_page + 1} / {_pageCount}";
        PreviousButton.IsEnabled = _page > 0;
        NextButton.IsEnabled = _page < _pageCount - 1;

        Cursor = Cursors.Wait;
        try
        {
            var png = PdfRenderer.Render(_path, _page, SKEncodedImageFormat.Png, quality: 100);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(png);
            bitmap.EndInit();
            bitmap.Freeze();

            PageImage.Source = bitmap;
            Scroller.ScrollToTop();
        }
        catch (Exception ex)
        {
            PageImage.Source = null;
            Report.Error($"Could not render page {_page + 1}.\n\n{ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void OnPrevious(object sender, RoutedEventArgs e) => ShowPage(_page - 1);

    private void OnNext(object sender, RoutedEventArgs e) => ShowPage(_page + 1);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left or Key.PageUp: ShowPage(_page - 1); e.Handled = true; break;
            case Key.Right or Key.PageDown or Key.Space: ShowPage(_page + 1); e.Handled = true; break;
            case Key.Home: ShowPage(0); e.Handled = true; break;
            case Key.End: ShowPage(_pageCount - 1); e.Handled = true; break;
            case Key.Escape: Close(); e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }
}
