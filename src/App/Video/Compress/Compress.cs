using System.Globalization;

namespace Flect.Video.Compress;

internal sealed class Compress(Request request) : VideoBatch(request)
{
    private long _target;

    public override string Title => Loc.T("menu.video.compress");

    public override bool Configure()
    {
        if (string.Equals(Request["preset"], "discord", StringComparison.OrdinalIgnoreCase))
        {
            _target = Preferences.Presets.CompressDiscord;
            return true;
        }

        var bytes = Request["bytes"];
        if (string.Equals(bytes, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var answer = TargetBytes.Ask(LargestSelected());
            if (answer is null) return false;
            _target = answer.Value;
            return true;
        }
        return long.TryParse(bytes, out _target) && _target > 0;
    }

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        long original = new FileInfo(path).Length;
        if (original <= Bitrate.SafeTarget(_target))
        {
            progress.Info(Loc.F("msg.alreadySmall", Path.GetFileName(path),
                                Flect.Display.Format.Bytes(original)));
            return;
        }

        var info = await Probe.ReadAsync(path, progress.Token);
        if (info.DurationSeconds <= 0)
        {
            throw new InvalidOperationException(Loc.T("msg.noDuration"));
        }

        var plan = Draft.Build(info, _target, Container.Resolve(Preferences));
        var destination = Flect.Files.OutputName.Derive(path, "_compressed",
            info.HasVideo ? plan.Extension : ".m4a");

        using var scratch = new Flect.Files.TempDir(destination);
        var pipeline = new Pipeline(plan, progress);

        var outcome = info.HasVideo
            ? await pipeline.RunAsync(path, scratch)
            : await pipeline.RunAudioOnlySourceAsync(path, scratch);

        File.Move(outcome.Path, destination);

        if (outcome.Bytes > _target)
        {
            progress.Warn(Loc.F("msg.overTarget", Path.GetFileName(path),
                                Flect.Display.Format.Bytes(outcome.Bytes), Flect.Display.Format.Bytes(_target)));
        }
    }
}
