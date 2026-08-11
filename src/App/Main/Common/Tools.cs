namespace Optimizer.Main;

internal static class Tools
{
    private static string InstallDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    public static string Ffmpeg => Resolve("ffmpeg.exe", "ffmpeg");
    public static string Ffprobe => Resolve("ffprobe.exe", "ffprobe");

    public static string CfrJar
    {
        get
        {
            var bundled = Path.Combine(InstallDirectory, "tools", "cfr.jar");
            if (File.Exists(bundled)) return bundled;
            throw new FileNotFoundException(
                "cfr.jar was not found in the tools folder. Run fetch/get.ps1 to download it.");
        }
    }

    public static string Java
    {
        get
        {
            var home = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(home))
            {
                var candidate = Path.Combine(home, "bin", "java.exe");
                if (File.Exists(candidate)) return candidate;
            }
            var onPath = FindOnPath("java.exe");
            if (onPath is not null) return onPath;

            throw new FileNotFoundException(
                "Java was not found. Decompiling a jar needs a Java runtime on PATH or JAVA_HOME.");
        }
    }

    private static string Resolve(string fileName, string displayName)
    {
        var bundled = Path.Combine(InstallDirectory, "tools", fileName);
        if (File.Exists(bundled)) return bundled;

        var onPath = FindOnPath(fileName);
        if (onPath is not null) return onPath;

        throw new FileNotFoundException(
            $"{displayName} was not found in the tools folder or on PATH. " +
            "Run fetch/get.ps1 to download the bundled tools.");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator,
                                             StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) {}
        }
        return null;
    }
}
