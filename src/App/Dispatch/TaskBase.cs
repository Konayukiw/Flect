namespace Flect.Dispatch;

internal abstract class TaskBase
{
    protected TaskBase(Request request)
    {
        Request = request;
        Flect.Files.OutputName.TaskId = request.Id;
    }

    protected Request Request { get; }

    public abstract string Title { get; }

    public virtual bool ShowsProgress => true;

    public virtual bool Configure() => true;

    public abstract Task RunAsync(ITaskProgress progress);

    public virtual string? Summary => null;

    public virtual void Present() { }
}
