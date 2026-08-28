using System.Globalization;
using System.Windows.Controls;

namespace Optimizer.Gui.Pages;

public partial class Video : UserControl, ISettingsPage
{
    private static readonly string[] SubtitleLanguages =
        ["ja", "en", "zh", "ko", "es", "fr", "de", "it", "pt", "ru", "ar", "th", "vi"];

    private HardwareEncoder _hardware = HardwareEncoder.None;

    public Video()
    {
        InitializeComponent();

        Fields.Fill(CodecBox,
            (VideoCodecChoice.H264, "settings.video.codec.h264"),
            (VideoCodecChoice.H265, "settings.video.codec.h265"),
            (VideoCodecChoice.Vp9, "settings.video.codec.vp9"),
            (VideoCodecChoice.Av1, "settings.video.codec.av1"));

        Fields.Fill(ContainerBox,
            (VideoContainer.Auto, "settings.video.container.auto"),
            (VideoContainer.Mp4, "settings.video.container.mp4"),
            (VideoContainer.Mkv, "settings.video.container.mkv"),
            (VideoContainer.WebM, "settings.video.container.webm"));

        Fields.Fill(GifDitherBox,
            (GifDither.None, "settings.video.gif.dither.none"),
            (GifDither.Bayer, "settings.video.gif.dither.bayer"),
            (GifDither.FloydSteinberg, "settings.video.gif.dither.floyd"));

        Fields.Fill(ThumbnailFormatBox,
            (ThumbnailFormat.Png, "settings.video.thumbnail.format.png"),
            (ThumbnailFormat.Jpg, "settings.video.thumbnail.format.jpg"));

        Fields.Fill(SubtitleModelBox,
            (SubtitleModel.Tiny, "settings.video.subtitles.model.tiny"),
            (SubtitleModel.Base, "settings.video.subtitles.model.base"),
            (SubtitleModel.Small, "settings.video.subtitles.model.small"),
            (SubtitleModel.Medium, "settings.video.subtitles.model.medium"),
            (SubtitleModel.LargeV3, "settings.video.subtitles.model.large"),
            (SubtitleModel.Turbo, "settings.video.subtitles.model.turbo"));

        Fields.Fill(SubtitleDeviceBox,
            (SubtitleDevice.Auto, "settings.video.subtitles.device.auto"),
            (SubtitleDevice.Cpu, "settings.video.subtitles.device.cpu"));

        FillLanguageBox(SubtitleLanguageBox);

        Fields.Fill(SubtitleOutputBox,
            (SubtitleOutput.Srt, "settings.video.subtitles.output.srt"),
            (SubtitleOutput.Vtt, "settings.video.subtitles.output.vtt"),
            (SubtitleOutput.Txt, "settings.video.subtitles.output.txt"));

        Fields.Fill(HardwareBox, (HardwareEncoder.None, "settings.video.hardware.none"));
    }

    private static void FillLanguageBox(ComboBox box)
    {
        var items = new List<(string Value, string Label)>
        {
            (string.Empty, Loc.T("settings.video.subtitles.language.auto")),
        };
        foreach (var code in SubtitleLanguages)
        {
            string label = code;
            try { label = CultureInfo.GetCultureInfo(code).NativeName; } catch (CultureNotFoundException) { }
            items.Add((code, label));
        }
        Fields.FillText(box, items);
    }

    void ISettingsPage.Load(Settings settings)
    {
        var video = settings.Video;
        var presets = video.Presets;

        Fields.ShowNumber(ResizeABox, presets.ResizeA);
        Fields.ShowNumber(ResizeBBox, presets.ResizeB);
        Fields.ShowBytes(DiscordBox, presets.CompressDiscord);
        Fields.ShowBytes(CompressABox, presets.CompressA);
        Fields.ShowBytes(CompressBBox, presets.CompressB);
        Fields.ShowBytes(CompressCBox, presets.CompressC);
        Fields.ShowNumber(RotateABox, presets.RotateA);
        Fields.ShowNumber(RotateBBox, presets.RotateB);
        Fields.ShowNumber(RotateCBox, presets.RotateC);

        Fields.Select(CodecBox, video.Codec);
        Fields.Select(ContainerBox, video.Container);

        _hardware = video.Hardware;
        LoadHardwareChoices();

        PrioritySpeedBox.IsChecked = video.Priority == EncodePriority.Speed;
        PriorityBalancedBox.IsChecked = video.Priority == EncodePriority.Balanced;
        PriorityQualityBox.IsChecked = video.Priority == EncodePriority.Quality;

        Fields.ShowInt(GifFpsBox, video.GifFps);
        Fields.ShowInt(GifWidthBox, video.GifMaxWidth);
        Fields.Select(GifDitherBox, video.GifDither);

        FillColorField.Value = video.RotateFillColor;

        TrimKeyframeBox.IsChecked = video.Trim.Accuracy == TrimAccuracy.Keyframe;
        TrimExactBox.IsChecked = video.Trim.Accuracy == TrimAccuracy.Exact;

        Fields.Select(ThumbnailFormatBox, video.Thumbnail.Format);
        Fields.ShowInt(ThumbnailQualityBox, video.Thumbnail.Quality);
        Fields.ShowInt(ThumbnailWidthBox, video.Thumbnail.MaxWidth);

        Fields.Select(SubtitleModelBox, video.Subtitles.Model);
        Fields.Select(SubtitleDeviceBox, video.Subtitles.Device);
        Fields.Select(SubtitleLanguageBox, video.Subtitles.Language ?? string.Empty);
        Fields.Select(SubtitleOutputBox, video.Subtitles.Output);
    }

    void ISettingsPage.Store(Settings settings)
    {
        var video = settings.Video;
        var presets = video.Presets;

        presets.ResizeA = Fields.Number(ResizeABox, presets.ResizeA, 1, 400);
        presets.ResizeB = Fields.Number(ResizeBBox, presets.ResizeB, 1, 400);
        presets.CompressDiscord = Fields.Bytes(DiscordBox, presets.CompressDiscord);
        presets.CompressA = Fields.Bytes(CompressABox, presets.CompressA);
        presets.CompressB = Fields.Bytes(CompressBBox, presets.CompressB);
        presets.CompressC = Fields.Bytes(CompressCBox, presets.CompressC);
        presets.RotateA = Fields.Number(RotateABox, presets.RotateA, -360, 360);
        presets.RotateB = Fields.Number(RotateBBox, presets.RotateB, -360, 360);
        presets.RotateC = Fields.Number(RotateCBox, presets.RotateC, -360, 360);

        video.Codec = Fields.Selected(CodecBox, video.Codec);
        video.Container = Fields.Selected(ContainerBox, video.Container);
        video.Hardware = Fields.Selected(HardwareBox, video.Hardware);

        video.Priority = PrioritySpeedBox.IsChecked == true ? EncodePriority.Speed
                       : PriorityQualityBox.IsChecked == true ? EncodePriority.Quality
                       : EncodePriority.Balanced;

        video.GifFps = Fields.Int(GifFpsBox, video.GifFps, 1, 50);
        video.GifMaxWidth = Fields.Int(GifWidthBox, video.GifMaxWidth, 64, 3840);
        video.GifDither = Fields.Selected(GifDitherBox, video.GifDither);

        video.RotateFillColor = FillColorField.Value;

        video.Trim.Accuracy = TrimExactBox.IsChecked == true
            ? TrimAccuracy.Exact
            : TrimAccuracy.Keyframe;

        video.Thumbnail.Format = Fields.Selected(ThumbnailFormatBox, video.Thumbnail.Format);
        video.Thumbnail.Quality = Fields.Int(ThumbnailQualityBox, video.Thumbnail.Quality, 1, 100);
        video.Thumbnail.MaxWidth = Fields.Int(ThumbnailWidthBox, video.Thumbnail.MaxWidth, 0, 3840);

        video.Subtitles.Model = Fields.Selected(SubtitleModelBox, video.Subtitles.Model);
        video.Subtitles.Device = Fields.Selected(SubtitleDeviceBox, video.Subtitles.Device);
        video.Subtitles.Language = Fields.Selected(SubtitleLanguageBox, string.Empty);
        video.Subtitles.Output = Fields.Selected(SubtitleOutputBox, video.Subtitles.Output);
    }

    private async void LoadHardwareChoices()
    {
        ShowHardware([]);

        var available = await Task.Run(Encoders.AvailableHardware);

        _hardware = Fields.Selected(HardwareBox, _hardware);
        ShowHardware(available);
    }

    private void ShowHardware(IReadOnlyList<HardwareEncoder> available)
    {
        var choices = new List<(HardwareEncoder, string)>
        {
            (HardwareEncoder.None, "settings.video.hardware.none"),
        };

        foreach (var hardware in available)
        {
            choices.Add((hardware, Key(hardware)));
        }

        if (_hardware != HardwareEncoder.None && !available.Contains(_hardware))
        {
            choices.Add((_hardware, Key(_hardware)));
        }

        Fields.Fill(HardwareBox, [.. choices]);
        Fields.Select(HardwareBox, _hardware);
    }

    private static string Key(HardwareEncoder hardware) => hardware switch
    {
        HardwareEncoder.Nvenc => "settings.video.hardware.nvenc",
        HardwareEncoder.Qsv => "settings.video.hardware.qsv",
        HardwareEncoder.Amf => "settings.video.hardware.amf",
        _ => "settings.video.hardware.none",
    };
}
