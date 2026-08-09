namespace Optimizer.Main;

internal sealed class WorkingFile(string path) : IDisposable
{
    private bool _keep;

    public string Path { get; } = path;

    public void Keep() => _keep = true;

    public void Dispose()
    {
        if (!_keep) OutputPath.SafeDelete(Path);
    }
}

internal sealed class ScratchDirectory : IDisposable
{
    public ScratchDirectory(string besidePath)
    {
        var parent = Path.GetDirectoryName(besidePath) ?? Directory.GetCurrentDirectory();
        SweepAbandoned(parent);

        Root = Path.Combine(parent, $".{Branding.Name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);

        try
        {
            File.SetAttributes(Root, FileAttributes.Directory | FileAttributes.Hidden);
        }
        catch (Exception) {}
    }

    public string Root { get; }

    public string PathFor(string name) => Path.Combine(Root, name);

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (Exception) {}
    }

    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(1);

    private static void SweepAbandoned(string parent)
    {
        try
        {
            var cutoff = DateTime.UtcNow - AbandonedAfter;
            foreach (var directory in Directory.EnumerateDirectories(parent, $".{Branding.Name}-*"))
            {
                if (Directory.GetCreationTimeUtc(directory) > cutoff) continue;
                try { Directory.Delete(directory, recursive: true); }
                catch (Exception) {}
            }
        }
        catch (Exception) {}
    }
}
