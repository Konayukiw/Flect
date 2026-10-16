using System.Runtime.CompilerServices;

namespace Evals.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Serial
{
    public const string Name = "Flect";
}

internal static class Paths
{
    public static string Repository { get; } = FindRepository();

    public static string Work { get; private set; } = string.Empty;

    public static string Bundled => Path.Combine(AppContext.BaseDirectory, "fixtures");

    public static bool OnCi => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    [ModuleInitializer]
    internal static void Initialize()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var tests = Path.Combine(Repository, "tests");
        if (Environment.GetEnvironmentVariable("FLECT_HOME") is not { Length: > 0 })
        {
            Environment.SetEnvironmentVariable("FLECT_HOME", Path.Combine(tests, "evals-home"));
        }

        Work = Environment.GetEnvironmentVariable("FLECT_EVALS_WORK") is { Length: > 0 } work
            ? Path.GetFullPath(work)
            : Path.Combine(tests, "evals-work");
        DeleteTree(Work);
        Directory.CreateDirectory(Work);
    }

    public static string NewFolder(string name)
    {
        var folder = Path.Combine(Work, $"{name}-{Guid.NewGuid():N}"[..(name.Length + 9)]);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static void DeleteTree(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch (IOException) { }
        }
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
    }

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Flect.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Flect.sln was not found above the test output.");
    }
}
