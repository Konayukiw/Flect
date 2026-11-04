namespace Flect.Subtitles;

internal sealed class SubtitlesSettings
{
    public SubtitleModel Model { get; set; } = SubtitleModel.Small;
    public SubtitleDevice Device { get; set; } = SubtitleDevice.Auto;
    public string Language { get; set; } = string.Empty;
    public SubtitleOutput Output { get; set; } = SubtitleOutput.Srt;
}
