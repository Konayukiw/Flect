using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Optimizer.Gui.Controls;

public sealed record ColorSwatch(string Hex, Brush Brush);

public partial class ColorField : UserControl
{
    private static readonly string[] Presets =
    [
        "#000000", "#FFFFFF", "#808080", "#C0C0C0",
        "#FF0000", "#FF8000", "#FFFF00", "#00FF00",
        "#00FFFF", "#0080FF", "#0000FF", "#FF00FF",
    ];

    private double _hue;
    private double _saturation;
    private double _brightness;
    private bool _dragging;
    private bool _suspend;

    public ColorField()
    {
        InitializeComponent();

        Swatches.ItemsSource = Presets
            .Select(hex => new ColorSwatch(hex, Frozen(ColorText.Parse(hex, Colors.Black))))
            .ToList();

        Pop.Opened += (_, _) => Dispatcher.BeginInvoke(PlaceMarker);
        SetColor(Colors.Black, updateHue: true);
    }

    public event EventHandler? ValueChanged;

    public string Value
    {
        get => ColorText.Format(Current);
        set => SetColor(ColorText.Parse(value, Colors.Black), updateHue: true);
    }

    private Color Current => FromHsv(_hue, _saturation, _brightness);

    private void SetColor(Color color, bool updateHue)
    {
        var (hue, saturation, brightness) = ToHsv(color);
        if (updateHue) _hue = hue;
        _saturation = saturation;
        _brightness = brightness;

        _suspend = true;
        HueSlider.Value = _hue;
        _suspend = false;

        Refresh();
    }

    private void Refresh()
    {
        var color = Current;
        var brush = Frozen(color);

        Swatch.Background = brush;
        Preview.Background = brush;
        HexLabel.Text = ColorText.Format(color);
        HueLayer.Fill = Frozen(FromHsv(_hue, 1, 1));
        Marker.Fill = brush;

        if (!HexBox.IsFocused) HexBox.Text = ColorText.Format(color);

        PlaceMarker();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PlaceMarker()
    {
        double width = Field.ActualWidth;
        double height = Field.ActualHeight;
        if (width <= 0 || height <= 0) return;

        Canvas.SetLeft(Marker, _saturation * width - Marker.Width / 2);
        Canvas.SetTop(Marker, (1 - _brightness) * height - Marker.Height / 2);
    }

    private void OnTriggerClick(object sender, MouseButtonEventArgs e) => Pop.IsOpen = !Pop.IsOpen;

    private void OnFieldDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        Field.CaptureMouse();
        PickFrom(e.GetPosition(Field));
    }

    private void OnFieldMove(object sender, MouseEventArgs e)
    {
        if (_dragging) PickFrom(e.GetPosition(Field));
    }

    private void OnFieldUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Field.ReleaseMouseCapture();
    }

    private void PickFrom(Point point)
    {
        double width = Math.Max(1, Field.ActualWidth);
        double height = Math.Max(1, Field.ActualHeight);

        _saturation = Math.Clamp(point.X / width, 0, 1);
        _brightness = Math.Clamp(1 - point.Y / height, 0, 1);
        Refresh();
    }

    private void OnHueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suspend) return;
        _hue = e.NewValue;
        Refresh();
    }

    private void OnHexKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitHex();
        e.Handled = true;
    }

    private void OnHexCommitted(object sender, RoutedEventArgs e) => CommitHex();

    private void CommitHex()
    {
        var parsed = ColorText.Parse(HexBox.Text, Current);
        SetColor(parsed, updateHue: true);
        HexBox.Text = ColorText.Format(Current);
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hex })
        {
            SetColor(ColorText.Parse(hex, Colors.Black), updateHue: true);
        }
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static (double Hue, double Saturation, double Brightness) ToHsv(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * (((g - b) / delta + 6) % 6);
            else if (max == g) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }
        return (hue, max <= 0 ? 0 : delta / max, max);
    }

    private static Color FromHsv(double hue, double saturation, double brightness)
    {
        double c = brightness * saturation;
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = brightness - c;

        (double r, double g, double b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(Channel(r + m), Channel(g + m), Channel(b + m));
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
