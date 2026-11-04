using System.Globalization;

namespace Flect.Audio;

internal sealed class Convert(Request request) : Flect.Dispatch.Batch(request)
{
    private string _target = "MP3";

    public override string Title => Loc.T("menu.audio.convert");

    public override bool Configure()
    {
        _target = (Request["to"] ?? "MP3").ToUpperInvariant();
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Flect.Video.Probe.ReadAsync(path, progress.Token);
        var output = Flect.Files.OutputName.Derive(path, string.Empty, Codec.Extension(_target));

        using var working = new Flect.Files.TempFile(output);
        await Ffmpeg.RunAsync(
            [.. Ffmpeg.Preamble, "-i", path, "-vn", .. Codec.Encoder(_target), output],
            progress, Loc.F("label.convertingTo", _target), info.DurationSeconds);
        working.Keep();
    }
}
