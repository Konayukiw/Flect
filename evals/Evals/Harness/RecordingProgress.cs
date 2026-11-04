using System.Reflection;
using System.Text;

namespace Evals.Harness;

internal sealed class RecordingProgress(CancellationToken token = default) : ITaskProgress
{
    private readonly Lock _gate = new();

    public List<string> Infos { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public CancellationToken Token => token;

    public void Status(string text)
    {
    }

    public void Step(int done, int total)
    {
    }

    public void Indeterminate()
    {
    }

    public void Info(string message)
    {
        lock (_gate) Infos.Add(message);
    }

    public void Warn(string message)
    {
        lock (_gate) Warnings.Add(message);
    }

    public void Error(string message)
    {
        lock (_gate) Errors.Add(message);
    }

    public void AssertClean() =>
        Assert.True(Errors.Count == 0, "The task reported errors: " + string.Join(" | ", Errors));
}
