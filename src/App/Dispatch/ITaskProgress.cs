namespace Flect.Dispatch;

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
