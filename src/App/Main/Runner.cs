using Optimizer.Gui;

namespace Optimizer.Main;

internal sealed class NullProgress : ITaskProgress
{
    public static readonly NullProgress Instance = new();

    public CancellationToken Token => CancellationToken.None;
    public void Status(string text) { }
    public void Step(int done, int total) { }
    public void Indeterminate() { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}

internal static class TaskRunner
{
    public static async Task RunAsync(TaskRequest request)
    {
        var task = TaskFactory.Create(request);
        if (task is null)
        {
            Report.Error($"Unrecognized command: {request.Id}");
            return;
        }

        if (!task.Configure()) return;

        if (!task.ShowsProgress)
        {
            await task.RunAsync(NullProgress.Instance);
            task.Present();
            return;
        }

        var window = new Progress(task.Title);
        window.Show();

        try
        {
            await task.RunAsync(window);
        }
        catch (OperationCanceledException)
        {
            window.Finish(TaskOutcome.Cancelled);
            return;
        }
        catch (Exception ex)
        {
            window.Error(ex.Message);
            window.Finish(TaskOutcome.Failed);
            return;
        }

        window.Finish(TaskOutcome.Succeeded, task.Summary);
        task.Present();
    }
}
