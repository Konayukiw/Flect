namespace Optimizer.Main.Common;

internal static class Tools
{
    private static string InstallDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    public static string Ffmpeg => Resolve("ffmpeg.exe", "ffmpeg");
    public static string Ffprobe => Resolve("ffprobe.exe", "ffprobe");

    public static string SevenZip => Resolve("7za.exe", "7-Zip");
    public static string SevenZipFull => Resolve("7z.exe", "7-Zip");

    public static string CfrJar
    {
        get
        {
            var bundled = Path.Combine(InstallDirectory, "deps", "cfr.jar");
            if (File.Exists(bundled)) return bundled;
            throw new FileNotFoundException(
                "cfr.jar was not found in the deps folder. Run fetch/get.ps1 to download it.");
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

    public static (string Executable, string[] PrefixArguments) Python
    {
        get
        {
            var venv = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Branding.Name, "env", "Scripts", "python.exe");
            if (File.Exists(venv)) return (venv, []);

            var onPath = FindOnPath("python.exe");
            if (onPath is not null) return (onPath, []);

            var python3 = FindOnPath("python3.exe");
            if (python3 is not null) return (python3, []);

            var localPrograms = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python");
            if (Directory.Exists(localPrograms))
            {
                try
                {
                    var installed = Directory.EnumerateFiles(localPrograms, "python.exe",
                                                              SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (installed is not null) return (installed, []);
                }
                catch (ArgumentException) {}
            }

            var launcher = FindOnPath("py.exe");
            if (launcher is not null) return (launcher, ["-3"]);

            throw new FileNotFoundException(
                "Python was not found. Obfuscating a Python file needs Python 3.10 or newer.");
        }
    }

    public static string PyObfuscateDirectory
    {
        get
        {
            var bundled = Path.Combine(InstallDirectory, "deps");
            if (File.Exists(Path.Combine(bundled, "pyobfuscate", "core.py"))) return bundled;

            var dir = new DirectoryInfo(InstallDirectory);
            for (int i = 0; i < 7 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "deps");
                if (File.Exists(Path.Combine(candidate, "pyobfuscate", "core.py"))) return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(
                "The pyobfuscate tool was not found in the deps folder.");
        }
    }

    private static string Resolve(string fileName, string displayName)
    {
        var bundled = Path.Combine(InstallDirectory, "deps", fileName);
        if (File.Exists(bundled)) return bundled;

        var onPath = FindOnPath(fileName);
        if (onPath is not null) return onPath;

        throw new FileNotFoundException(
            $"{displayName} was not found in the deps folder or on PATH. " +
            "Run fetch/get.ps1 to download the bundled dependencies.");
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
