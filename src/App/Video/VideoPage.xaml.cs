using System.Globalization;
using System.Windows.Controls;

namespace Flect.Video;

public partial class VideoPage : UserControl, ISettings
{
    private static readonly string[] SubtitleLanguages =
        ["ja", "en", "zh", "ko", "es", "fr", "de", "it", "pt", "ru", "ar", "th", "vi"];

    private HardwareEncoder _hardware = HardwareEncoder.None;

    public VideoPage()
    {
        InitializeComponent();

        FillCodecs(VideoCodecChoice.H264);

        Flect.Settings.Binding.Fill(ContainerBox,
            (VideoContainer.Auto, "settings.video.container.auto"),
            (VideoContainer.Mp4, "settings.video.container.mp4"),
            (VideoContainer.Mkv, "settings.video.container.mkv"),
            (VideoContainer.WebM, "settings.video.container.webm"));

        Flect.Settings.Binding.Fill(GifDitherBox,
            (GifDither.None, "settings.video.gif.dither.none"),
            (GifDither.Bayer, "settings.video.gif.dither.bayer"),
            (GifDither.FloydSteinberg, "settings.video.gif.dither.floyd"));

        Flect.Settings.Binding.Fill(ThumbnailFormatBox,
            (ThumbnailFormat.Png, "settings.video.thumbnail.format.png"),
            (ThumbnailFormat.Jpg, "settings.video.thumbnail.format.jpg"));

        Flect.Settings.Binding.Fill(SubtitleModelBox,
            (SubtitleModel.Tiny, "settings.video.subtitles.model.tiny"),
            (SubtitleModel.Base, "settings.video.subtitles.model.base"),
            (SubtitleModel.Small, "settings.video.subtitles.model.small"),
            (SubtitleModel.Medium, "settings.video.subtitles.model.medium"),
            (SubtitleModel.LargeV3, "settings.video.subtitles.model.large"),
            (SubtitleModel.Turbo, "settings.video.subtitles.model.turbo"));

        Flect.Settings.Binding.Fill(SubtitleDeviceBox,
            (SubtitleDevice.Auto, "settings.video.subtitles.device.auto"),
            (SubtitleDevice.Cpu, "settings.video.subtitles.device.cpu"));

        FillLanguageBox(SubtitleLanguageBox);

        Flect.Settings.Binding.Fill(SubtitleOutputBox,
            (SubtitleOutput.Srt, "settings.video.subtitles.output.srt"),
            (SubtitleOutput.Vtt, "settings.video.subtitles.output.vtt"),
            (SubtitleOutput.Txt, "settings.video.subtitles.output.txt"));

        Flect.Settings.Binding.Fill(HardwareBox, (HardwareEncoder.None, "settings.video.hardware.none"));
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
        Flect.Settings.Binding.FillText(box, items);
    }

    private void FillCodecs(VideoCodecChoice selected)
    {
        bool webm = Flect.Settings.Binding.Selected(ContainerBox, VideoContainer.Auto) == VideoContainer.WebM;

        var choices = new List<(VideoCodecChoice, string)>();
        if (!webm)
        {
            choices.Add((VideoCodecChoice.H264, "settings.video.codec.h264"));
            choices.Add((VideoCodecChoice.H265, "settings.video.codec.h265"));
        }
        choices.Add((VideoCodecChoice.Vp9, "settings.video.codec.vp9"));
        choices.Add((VideoCodecChoice.Av1, "settings.video.codec.av1"));

        Flect.Settings.Binding.Fill(CodecBox, [.. choices]);
        Flect.Settings.Binding.Select(CodecBox, webm ? Container.CodecFor(VideoContainer.WebM, selected) : selected);
    }

    private void OnContainerChanged(object sender, SelectionChangedEventArgs e) =>
        FillCodecs(Flect.Settings.Binding.Selected(CodecBox, VideoCodecChoice.H264));

    void ISettings.Load(Preferences settings)
    {
        var video = settings.Video;
        var presets = video.Presets;

        Flect.Settings.Binding.ShowNumber(ResizeABox, presets.ResizeA);
        Flect.Settings.Binding.ShowNumber(ResizeBBox, presets.ResizeB);
        Flect.Settings.Binding.ShowBytes(DiscordBox, presets.CompressDiscord);
        Flect.Settings.Binding.ShowBytes(CompressABox, presets.CompressA);
        Flect.Settings.Binding.ShowBytes(CompressBBox, presets.CompressB);
        Flect.Settings.Binding.ShowBytes(CompressCBox, presets.CompressC);
        Flect.Settings.Binding.ShowNumber(RotateABox, presets.RotateA);
        Flect.Settings.Binding.ShowNumber(RotateBBox, presets.RotateB);
        Flect.Settings.Binding.ShowNumber(RotateCBox, presets.RotateC);

        Flect.Settings.Binding.Select(CodecBox, video.Codec);
        Flect.Settings.Binding.Select(ContainerBox, video.Container);

        _hardware = video.Hardware;
        LoadHardwareChoices();

        PrioritySpeedBox.IsChecked = video.Priority == EncodePriority.Speed;
        PriorityBalancedBox.IsChecked = video.Priority == EncodePriority.Balanced;
        PriorityQualityBox.IsChecked = video.Priority == EncodePriority.Quality;

        Flect.Settings.Binding.ShowInt(GifFpsBox, video.GifFps);
        Flect.Settings.Binding.ShowInt(GifWidthBox, video.GifMaxWidth);
        Flect.Settings.Binding.Select(GifDitherBox, video.GifDither);

        FillColorField.Value = video.RotateFillColor;

        TrimKeyframeBox.IsChecked = video.Trim.Accuracy == TrimAccuracy.Keyframe;
        TrimExactBox.IsChecked = video.Trim.Accuracy == TrimAccuracy.Exact;

        Flect.Settings.Binding.Select(ThumbnailFormatBox, video.Thumbnail.Format);
        Flect.Settings.Binding.ShowInt(ThumbnailQualityBox, video.Thumbnail.Quality);
        Flect.Settings.Binding.ShowInt(ThumbnailWidthBox, video.Thumbnail.MaxWidth);

        Flect.Settings.Binding.Select(SubtitleModelBox, video.Subtitles.Model);
        Flect.Settings.Binding.Select(SubtitleDeviceBox, video.Subtitles.Device);
        Flect.Settings.Binding.Select(SubtitleLanguageBox, video.Subtitles.Language ?? string.Empty);
        Flect.Settings.Binding.Select(SubtitleOutputBox, video.Subtitles.Output);
    }

    void ISettings.Store(Preferences settings)
    {
        var video = settings.Video;
        var presets = video.Presets;

        presets.ResizeA = Flect.Settings.Binding.Number(ResizeABox, presets.ResizeA, 1, 400);
        presets.ResizeB = Flect.Settings.Binding.Number(ResizeBBox, presets.ResizeB, 1, 400);
        presets.CompressDiscord = Flect.Settings.Binding.Bytes(DiscordBox, presets.CompressDiscord);
        presets.CompressA = Flect.Settings.Binding.Bytes(CompressABox, presets.CompressA);
        presets.CompressB = Flect.Settings.Binding.Bytes(CompressBBox, presets.CompressB);
        presets.CompressC = Flect.Settings.Binding.Bytes(CompressCBox, presets.CompressC);
        presets.RotateA = Flect.Settings.Binding.Number(RotateABox, presets.RotateA, -360, 360);
        presets.RotateB = Flect.Settings.Binding.Number(RotateBBox, presets.RotateB, -360, 360);
        presets.RotateC = Flect.Settings.Binding.Number(RotateCBox, presets.RotateC, -360, 360);

        video.Codec = Flect.Settings.Binding.Selected(CodecBox, video.Codec);
        video.Container = Flect.Settings.Binding.Selected(ContainerBox, video.Container);
        video.Hardware = Flect.Settings.Binding.Selected(HardwareBox, video.Hardware);

        video.Priority = PrioritySpeedBox.IsChecked == true ? EncodePriority.Speed
                       : PriorityQualityBox.IsChecked == true ? EncodePriority.Quality
                       : EncodePriority.Balanced;

        video.GifFps = Flect.Settings.Binding.Int(GifFpsBox, video.GifFps, 1, 50);
        video.GifMaxWidth = Flect.Settings.Binding.Int(GifWidthBox, video.GifMaxWidth, 64, 3840);
        video.GifDither = Flect.Settings.Binding.Selected(GifDitherBox, video.GifDither);

        video.RotateFillColor = FillColorField.Value;

        video.Trim.Accuracy = TrimExactBox.IsChecked == true
            ? TrimAccuracy.Exact
            : TrimAccuracy.Keyframe;

        video.Thumbnail.Format = Flect.Settings.Binding.Selected(ThumbnailFormatBox, video.Thumbnail.Format);
        video.Thumbnail.Quality = Flect.Settings.Binding.Int(ThumbnailQualityBox, video.Thumbnail.Quality, 1, 100);
        video.Thumbnail.MaxWidth = Flect.Settings.Binding.Int(ThumbnailWidthBox, video.Thumbnail.MaxWidth, 0, 3840);

        video.Subtitles.Model = Flect.Settings.Binding.Selected(SubtitleModelBox, video.Subtitles.Model);
        video.Subtitles.Device = Flect.Settings.Binding.Selected(SubtitleDeviceBox, video.Subtitles.Device);
        video.Subtitles.Language = Flect.Settings.Binding.Selected(SubtitleLanguageBox, string.Empty);
        video.Subtitles.Output = Flect.Settings.Binding.Selected(SubtitleOutputBox, video.Subtitles.Output);
    }

    private async void LoadHardwareChoices()
    {
        ShowHardware([]);

        var available = await Task.Run(Encoder.AvailableHardware);

        _hardware = Flect.Settings.Binding.Selected(HardwareBox, _hardware);
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

        Flect.Settings.Binding.Fill(HardwareBox, [.. choices]);
        Flect.Settings.Binding.Select(HardwareBox, _hardware);
    }

    private static string Key(HardwareEncoder hardware) => hardware switch
    {
        HardwareEncoder.Nvenc => "settings.video.hardware.nvenc",
        HardwareEncoder.Qsv => "settings.video.hardware.qsv",
        HardwareEncoder.Amf => "settings.video.hardware.amf",
        _ => "settings.video.hardware.none",
    };
}
