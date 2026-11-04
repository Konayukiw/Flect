using System.Globalization;
using ImageMagick;

namespace Flect.Image;

internal sealed class Cutout(Request request) : TaskBase(request)
{
    public override string Title => Loc.T("menu.image.chromakey");

    public override async Task RunAsync(ITaskProgress progress)
    {
        var prefs = Preferences.Current.Image;
        var mode = prefs.BackgroundMode;
        var jobs = Request.Paths.Select(path =>
            (Input: path, Output: Flect.Files.OutputName.Derive(path, "_keyed", ".png"))).ToList();

        if (mode == BackgroundRemovalMode.ChromaKeyOnly)
        {
            await RunChromaKeyOnlyAsync(jobs, prefs, progress).ConfigureAwait(false);
            return;
        }

        bool aiSucceeded = false;
        Exception? aiException = null;
        IReadOnlyList<Matte.FileResult>? aiResults = null;

        try
        {
            progress.Indeterminate();
            var batch = await Matte.RemoveBatchAsync(
                jobs.Select(j => (j.Input, j.Output)).ToList(),
                progress, progress.Token).ConfigureAwait(false);
            aiResults = batch.Files;
            aiSucceeded = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            aiException = ex;
            progress.Warn($"{Loc.T("msg.bgAiFailed")}: {ex.Message}");
        }

        if (mode == BackgroundRemovalMode.AiOnly)
        {
            if (!aiSucceeded)
            {
                foreach (var job in jobs)
                    progress.Error($"{Path.GetFileName(job.Input)} - {aiException?.Message ?? Loc.T("msg.bgAiFailed")}");
                return;
            }
            foreach (var r in aiResults!)
            {
                progress.Token.ThrowIfCancellationRequested();
                if (r.Ok && File.Exists(r.Output))
                {
                    continue;
                }
                else
                {
                    try { if (File.Exists(r.Output)) File.Delete(r.Output); } catch { }
                    progress.Error($"{Path.GetFileName(r.Input)} - {r.Error ?? Loc.T("msg.bgAiFailed")}");
                }
            }
            return;
        }

        if (!aiSucceeded)
        {
            progress.Warn(Loc.T("msg.bgFallingBack"));
            foreach (var job in jobs)
            {
                progress.Token.ThrowIfCancellationRequested();
                progress.Status(Path.GetFileName(job.Input));
                try { await RunSingleChromaKeyAsync(job.Input, job.Output, prefs, progress).ConfigureAwait(false); }
                catch (Exception ex) { progress.Error($"{Path.GetFileName(job.Input)} - {ex.Message}"); }
            }
            return;
        }

        foreach (var r in aiResults!)
        {
            progress.Token.ThrowIfCancellationRequested();
            progress.Status(Path.GetFileName(r.Input));
            if (r.Ok && File.Exists(r.Output))
            {
                continue;
            }
            try
            {
                if (File.Exists(r.Output)) try { File.Delete(r.Output); } catch { }
                progress.Warn($"{Path.GetFileName(r.Input)} - {Loc.T("msg.bgFallingBackSingle")}");
                await RunSingleChromaKeyAsync(r.Input, r.Output, prefs, progress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress.Error($"{Path.GetFileName(r.Input)} - {ex.Message}");
            }
        }
    }

    private static Task RunChromaKeyOnlyAsync(
        IReadOnlyList<(string Input, string Output)> jobs,
        ImageSettings prefs,
        ITaskProgress progress)
    {
        return Task.Run(() =>
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                progress.Token.ThrowIfCancellationRequested();
                progress.Step(i, jobs.Count);
                var (input, output) = jobs[i];
                progress.Status(Path.GetFileName(input));
                try
                {
                    Matte.ApplyChromaKey(
                        input, output, prefs.BackgroundKeyColor, prefs.BackgroundTolerance);
                }
                catch (Exception ex)
                {
                    progress.Error($"{Path.GetFileName(input)} - {ex.Message}");
                    try { if (File.Exists(output)) File.Delete(output); } catch { }
                }
            }
            progress.Step(jobs.Count, jobs.Count);
        }, progress.Token);
    }

    private static Task RunSingleChromaKeyAsync(string input, string output, ImageSettings prefs, ITaskProgress progress)
    {
        return Task.Run(() =>
        {
            using var working = new Flect.Files.TempFile(output);
            Matte.ApplyChromaKey(
                input, output, prefs.BackgroundKeyColor, prefs.BackgroundTolerance);
            working.Keep();
        }, progress.Token);
    }
}
