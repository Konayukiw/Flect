namespace Flect.Dispatch;

internal static class Session
{
    public static async Task RunAsync(Request request)
    {
        var task = Dispatch.Create(request);
        if (task is null)
        {
            Flect.Display.Notice.Error($"Unrecognized command: {request.Id}");
            return;
        }

        if (!task.Configure()) return;

        if (!task.ShowsProgress)
        {
            await task.RunAsync(Silent.Instance);
            task.Present();
            return;
        }

        var window = new ProgressWindow(task.Title);
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
