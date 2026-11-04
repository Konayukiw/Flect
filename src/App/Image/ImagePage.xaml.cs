using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Flect.Image;

public partial class ImagePage : UserControl, ISettings
{
    public ImagePage() => InitializeComponent();

    void ISettings.Load(Preferences settings)
    {
        var image = settings.Image;
        var presets = image.Presets;

        Flect.Settings.Binding.ShowNumber(ResizeABox, presets.ResizeA);
        Flect.Settings.Binding.ShowNumber(ResizeBBox, presets.ResizeB);
        Flect.Settings.Binding.ShowBytes(CompressABox, presets.CompressA);
        Flect.Settings.Binding.ShowBytes(CompressBBox, presets.CompressB);
        Flect.Settings.Binding.ShowBytes(CompressCBox, presets.CompressC);
        Flect.Settings.Binding.ShowBytes(CompressDBox, presets.CompressD);
        Flect.Settings.Binding.ShowNumber(RotateABox, presets.RotateA);
        Flect.Settings.Binding.ShowNumber(RotateBBox, presets.RotateB);
        Flect.Settings.Binding.ShowNumber(RotateCBox, presets.RotateC);

        WebpFallbackBox.IsChecked = image.AllowWebpFallback;
        WebpQualitySlider.Value = image.WebpQuality;

        BackgroundModeBox.SelectedIndex = image.BackgroundMode switch
        {
            BackgroundRemovalMode.AiOnly => 1,
            BackgroundRemovalMode.ChromaKeyOnly => 2,
            _ => 0,
        };
        KeyColorField.Value = image.BackgroundKeyColor;
        ToleranceSlider.Value = image.BackgroundTolerance;
        UpdateBackgroundModeUi();

        MetaExifBox.IsChecked = image.Metadata.RemoveExif;
        MetaGpsBox.IsChecked = image.Metadata.RemoveGps;
        MetaCommentsBox.IsChecked = image.Metadata.RemoveComments;
        MetaProfileBox.IsChecked = image.Metadata.RemoveColorProfile;
        MetaOrientationBox.IsChecked = image.Metadata.ApplyOrientation;
    }

    void ISettings.Store(Preferences settings)
    {
        var image = settings.Image;
        var presets = image.Presets;

        presets.ResizeA = Flect.Settings.Binding.Number(ResizeABox, presets.ResizeA, 1, 400);
        presets.ResizeB = Flect.Settings.Binding.Number(ResizeBBox, presets.ResizeB, 1, 400);
        presets.CompressA = Flect.Settings.Binding.Bytes(CompressABox, presets.CompressA);
        presets.CompressB = Flect.Settings.Binding.Bytes(CompressBBox, presets.CompressB);
        presets.CompressC = Flect.Settings.Binding.Bytes(CompressCBox, presets.CompressC);
        presets.CompressD = Flect.Settings.Binding.Bytes(CompressDBox, presets.CompressD);
        presets.RotateA = Flect.Settings.Binding.Number(RotateABox, presets.RotateA, -360, 360);
        presets.RotateB = Flect.Settings.Binding.Number(RotateBBox, presets.RotateB, -360, 360);
        presets.RotateC = Flect.Settings.Binding.Number(RotateCBox, presets.RotateC, -360, 360);

        image.AllowWebpFallback = WebpFallbackBox.IsChecked == true;
        image.WebpQuality = (int)Math.Round(WebpQualitySlider.Value);

        image.BackgroundMode = BackgroundModeBox.SelectedIndex switch
        {
            1 => BackgroundRemovalMode.AiOnly,
            2 => BackgroundRemovalMode.ChromaKeyOnly,
            _ => BackgroundRemovalMode.AiWithFallback,
        };
        image.BackgroundKeyColor = KeyColorField.Value;
        image.BackgroundTolerance = Math.Round(ToleranceSlider.Value);

        image.Metadata.RemoveExif = MetaExifBox.IsChecked == true;
        image.Metadata.RemoveGps = MetaGpsBox.IsChecked == true;
        image.Metadata.RemoveComments = MetaCommentsBox.IsChecked == true;
        image.Metadata.RemoveColorProfile = MetaProfileBox.IsChecked == true;
        image.Metadata.ApplyOrientation = MetaOrientationBox.IsChecked == true;
    }

    private void OnWebpQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WebpQualityLabel is null) return;
        WebpQualityLabel.Text = Math.Round(e.NewValue).ToString(CultureInfo.CurrentCulture);
    }

    private void OnToleranceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ToleranceLabel is null) return;
        ToleranceLabel.Text = Math.Round(e.NewValue).ToString(CultureInfo.CurrentCulture) + "%";
    }

    private void OnBackgroundModeChanged(object sender, SelectionChangedEventArgs e) => UpdateBackgroundModeUi();

    private void UpdateBackgroundModeUi()
    {
        if (BackgroundModeBox is null || KeyColorField is null || ToleranceSlider is null) return;
        var mode = BackgroundModeBox.SelectedIndex switch
        {
            1 => BackgroundRemovalMode.AiOnly,
            2 => BackgroundRemovalMode.ChromaKeyOnly,
            _ => BackgroundRemovalMode.AiWithFallback,
        };
        var isAiOnly = mode == BackgroundRemovalMode.AiOnly;
        var isAiFallback = mode == BackgroundRemovalMode.AiWithFallback;
        KeyColorField.IsEnabled = !isAiOnly;
        ToleranceSlider.IsEnabled = !isAiOnly;
        if (ToleranceLabel is not null) ToleranceLabel.Opacity = isAiOnly ? 0.4 : 1.0;
        if (ChromaFallbackPanel is not null) ChromaFallbackPanel.Visibility = isAiOnly ? Visibility.Collapsed : Visibility.Visible;
        if (FallbackHint is not null) FallbackHint.Visibility = isAiFallback ? Visibility.Visible : Visibility.Collapsed;
        if (AiOnlyHint is not null) AiOnlyHint.Visibility = isAiOnly ? Visibility.Visible : Visibility.Collapsed;
    }
}
