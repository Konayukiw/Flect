namespace Flect.Dispatch;

internal abstract class Batch(Request request) : TaskBase(request)
{
    protected abstract Task ProcessAsync(string path, ITaskProgress progress);

    public override async Task RunAsync(ITaskProgress progress)
    {
        var paths = Request.Paths;
        for (int index = 0; index < paths.Count; index++)
        {
            progress.Token.ThrowIfCancellationRequested();
            progress.Step(index, paths.Count);
            progress.Status(Path.GetFileName(paths[index]));

            try
            {
                await ProcessAsync(paths[index], progress);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress.Error($"{Path.GetFileName(paths[index])} - {ex.Message}");
            }
        }
        progress.Step(paths.Count, paths.Count);
    }
}
