using System.Globalization;

namespace Flect.Video;

internal sealed class Resize(Request request) : VideoBatch(request)
{
    private PixelTarget? _pixels;
    private double _percent = 50;

    public override string Title => Loc.T("menu.video.resize");

    public override bool Configure()
    {
        var scale = Request["scale"];
        if (!string.Equals(scale, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(scale, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                value > 0)
            {
                _percent = value;
            }
            return true;
        }
        _pixels = Pixels.Ask();
        return _pixels is not null;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Probe.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var output = Flect.Files.OutputName.Derive(path, "_resized");
        await FilterAsync(path, output, ScaleFilter(), info, progress, Loc.T("label.resizing"));
    }

    private string ScaleFilter()
    {
        if (_pixels is null)
        {
            var factor = (_percent / 100).ToString(CultureInfo.InvariantCulture);
            return $"scale=trunc(iw*{factor}/2)*2:trunc(ih*{factor}/2)*2";
        }

        uint width = _pixels.Width;
        uint height = _pixels.Height;

        if (width > 0 && height > 0)
        {
            return _pixels.KeepAspect
                ? $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                  Ffmpeg.EvenDimensions
                : $"scale={width - width % 2}:{height - height % 2}";
        }
        return width > 0 ? $"scale={width - width % 2}:-2" : $"scale=-2:{height - height % 2}";
    }
}
