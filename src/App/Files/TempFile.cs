namespace Flect.Files;

internal sealed class TempFile(string path) : IDisposable
{
    private bool _keep;

    public string Path { get; } = path;

    public void Keep() => _keep = true;

    public void Dispose()
    {
        if (!_keep) OutputName.SafeDelete(Path);
    }
}
