using System.Globalization;

namespace Flect.Audio;

internal sealed class Extract(Request request) : Flect.Dispatch.Batch(request)
{
    private string _target = "MP3";

    public override string Title => Loc.T("menu.video.extractAudio");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP3").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Flect.Video.Probe.ReadAsync(path, progress.Token);
        if (!info.HasAudio) throw new InvalidOperationException(Loc.T("msg.noAudioTrack"));

        var output = Flect.Files.OutputName.Derive(path, string.Empty, Codec.Extension(_target));
        using var working = new Flect.Files.TempFile(output);

        var copyable = Codec.CopyableFrom(_target);
        if (copyable is not null && string.Equals(info.AudioCodec, copyable,
                                                  StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await Ffmpeg.RunAsync(
                    [.. Ffmpeg.Preamble, "-i", path, "-vn", "-c:a", "copy", output],
                    progress, Loc.F("label.extracting", _target), info.DurationSeconds);
                working.Keep();
                return;
            }
            catch (InvalidOperationException)
            {
                Flect.Files.OutputName.SafeDelete(output);
            }
        }

        await Ffmpeg.RunAsync(
            [.. Ffmpeg.Preamble, "-i", path, "-vn", .. Codec.Encoder(_target), output],
            progress, Loc.F("label.extracting", _target), info.DurationSeconds);
        working.Keep();
    }
}
