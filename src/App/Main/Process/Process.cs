namespace Optimizer.Main.Process;

internal interface ITaskProgress
{
    CancellationToken Token { get; }

    void Status(string text);

    void Step(int done, int total);
    void Indeterminate();

    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

internal abstract class TaskBase(Request request)
{
    protected Request Request { get; } = request;

    public abstract string Title { get; }

    public virtual bool ShowsProgress => true;

    public virtual bool Configure() => true;

    public abstract Task RunAsync(ITaskProgress progress);

    public virtual string? Summary => null;

    public virtual void Present() { }
}

internal abstract class BatchTask(Request request) : TaskBase(request)  
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
                progress.Error($"{Path.GetFileName(paths[index])} — {ex.Message}");
            }
        }
        progress.Step(paths.Count, paths.Count);
    }
}
