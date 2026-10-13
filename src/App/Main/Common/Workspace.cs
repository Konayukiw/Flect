namespace Optimizer.Main.Common;

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
    private const string LockName = ".lock";

    private readonly FileStream _lock;

    public ScratchDirectory(string besidePath)
    {
        var parent = Path.GetDirectoryName(besidePath) ?? Directory.GetCurrentDirectory();
        SweepAbandoned(parent);

        Root = Path.Combine(parent, $".{Branding.Name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        _lock = new FileStream(Path.Combine(Root, LockName), FileMode.CreateNew, FileAccess.Write,
                               FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);

        try
        {
            File.SetAttributes(Root, FileAttributes.Directory | FileAttributes.Hidden);
        }
        catch (Exception) {}
    }

    public string Root { get; }

    public string PathFor(string name) => Path.Combine(Root, name);

    public IEnumerable<string> Files() =>
        Directory.EnumerateFiles(Root).Where(file => Path.GetFileName(file) != LockName);

    public void Dispose()
    {
        _lock.Dispose();
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
                if (InUse(directory)) continue;
                try { Directory.Delete(directory, recursive: true); }
                catch (Exception) {}
            }
        }
        catch (Exception) {}
    }

    private static bool InUse(string directory)
    {
        try
        {
            File.Delete(Path.Combine(directory, LockName));
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
