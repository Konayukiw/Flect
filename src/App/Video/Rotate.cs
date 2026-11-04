using System.Globalization;

namespace Flect.Video;

internal sealed class Rotate(Request request) : VideoBatch(request)
{
    private double _degrees = 90;

    public override string Title => Loc.T("menu.video.rotate");

    public override bool Configure()
    {
        if (double.TryParse(Request["deg"], NumberStyles.Float, CultureInfo.InvariantCulture,
                            out var value) && double.IsFinite(value))
        {
            _degrees = value;
        }
        return true;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var info = await Probe.ReadAsync(path, progress.Token);
        RequireVideo(info);

        var output = Flect.Files.OutputName.Derive(path, "_rotated");
        await FilterAsync(path, output, RotateFilter(), info, progress, Loc.T("label.rotating"));
    }

    private string RotateFilter()
    {
        double turn = (_degrees % 360 + 360) % 360;
        if (turn == 90) return $"transpose=1,{Ffmpeg.EvenDimensions}";
        if (turn == 180) return $"transpose=1,transpose=1,{Ffmpeg.EvenDimensions}";
        if (turn == 270) return $"transpose=2,{Ffmpeg.EvenDimensions}";

        var angle = _degrees.ToString(CultureInfo.InvariantCulture) + "*PI/180";
        return $"rotate={angle}:ow=rotw({angle}):oh=roth({angle}):" +
               $"fillcolor={HexColor.ForFfmpeg(Preferences.RotateFillColor)}," +
               Ffmpeg.EvenDimensions;
    }
}
