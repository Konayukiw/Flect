using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class ImagePage : UserControl, ISettingsPage
{
    public ImagePage() => InitializeComponent();

    void ISettingsPage.Load(Settings settings)
    {
        var image = settings.Image;
        var presets = image.Presets;

        Fields.ShowNumber(ResizeABox, presets.ResizeA);
        Fields.ShowNumber(ResizeBBox, presets.ResizeB);
        Fields.ShowBytes(CompressABox, presets.CompressA);
        Fields.ShowBytes(CompressBBox, presets.CompressB);
        Fields.ShowBytes(CompressCBox, presets.CompressC);
        Fields.ShowBytes(CompressDBox, presets.CompressD);
        Fields.ShowNumber(RotateABox, presets.RotateA);
        Fields.ShowNumber(RotateBBox, presets.RotateB);
        Fields.ShowNumber(RotateCBox, presets.RotateC);

        WebpFallbackBox.IsChecked = image.AllowWebpFallback;
        WebpQualitySlider.Value = image.WebpQuality;

        KeyColorField.Value = image.BackgroundKeyColor;
        ToleranceSlider.Value = image.BackgroundTolerance;

        MetaExifBox.IsChecked = image.Metadata.RemoveExif;
        MetaGpsBox.IsChecked = image.Metadata.RemoveGps;
        MetaCommentsBox.IsChecked = image.Metadata.RemoveComments;
        MetaProfileBox.IsChecked = image.Metadata.RemoveColorProfile;
        MetaOrientationBox.IsChecked = image.Metadata.ApplyOrientation;
    }

    void ISettingsPage.Store(Settings settings)
    {
        var image = settings.Image;
        var presets = image.Presets;

        presets.ResizeA = Fields.Number(ResizeABox, presets.ResizeA, 1, 400);
        presets.ResizeB = Fields.Number(ResizeBBox, presets.ResizeB, 1, 400);
        presets.CompressA = Fields.Bytes(CompressABox, presets.CompressA);
        presets.CompressB = Fields.Bytes(CompressBBox, presets.CompressB);
        presets.CompressC = Fields.Bytes(CompressCBox, presets.CompressC);
        presets.CompressD = Fields.Bytes(CompressDBox, presets.CompressD);
        presets.RotateA = Fields.Number(RotateABox, presets.RotateA, -360, 360);
        presets.RotateB = Fields.Number(RotateBBox, presets.RotateB, -360, 360);
        presets.RotateC = Fields.Number(RotateCBox, presets.RotateC, -360, 360);

        image.AllowWebpFallback = WebpFallbackBox.IsChecked == true;
        image.WebpQuality = (int)Math.Round(WebpQualitySlider.Value);

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
}
