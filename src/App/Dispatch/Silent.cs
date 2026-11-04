namespace Flect.Dispatch;

internal sealed class Silent : ITaskProgress
{
    public static readonly Silent Instance = new();

    public CancellationToken Token => CancellationToken.None;
    public void Status(string text) { }
    public void Step(int done, int total) { }
    public void Indeterminate() { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}
