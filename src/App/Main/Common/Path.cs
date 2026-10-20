namespace Optimizer.Main.Common;

internal static class OutputPath
{
    private static readonly AsyncLocal<string?> CurrentTask = new();

    public static string? TaskId
    {
        get => CurrentTask.Value;
        set => CurrentTask.Value = value;
    }

    public static string Derive(string source, string suffix = "", string? extension = null)
    {
        var directory = DirectoryFor(source);
        var stem = Path.GetFileNameWithoutExtension(source) + suffix;

        var ext = extension ?? Path.GetExtension(source);
        if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;

        var candidate = Path.Combine(directory, stem + ext);
        for (int n = 2; Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{stem} ({n}){ext}");
        }
        return candidate;
    }

    public static string DeriveDirectory(string source, string? stem = null, string suffix = "")
    {
        var directory = DirectoryFor(source);
        var baseName = (stem ?? Path.GetFileNameWithoutExtension(source)) + suffix;

        var candidate = Path.Combine(directory, baseName);
        for (int n = 2; Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{baseName} ({n})");
        }
        return candidate;
    }

    public static string DirectoryFor(string source)
    {
        var fallback = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        var key = OutputCatalog.KeyFor(TaskId);
        var folders = Settings.Current.Output?.Folders;
        if (key is null || folders is null || !folders.TryGetValue(key, out var text)) return fallback;

        text = text.Trim().Trim('"');
        if (text.Length == 0) return fallback;

        var expanded = Environment.ExpandEnvironmentVariables(text);
        var target = Path.IsPathRooted(expanded) ? expanded : Path.Combine(fallback, expanded);
        target = Path.GetFullPath(target);

        if (Directory.Exists(source) && IsInside(target, source))
        {
            throw new InvalidOperationException(Loc.T("msg.outputInsideSource"));
        }

        Directory.CreateDirectory(target);
        return target;
    }

    public static string Scratch(string finalPath, string extension)
    {
        var directory = Path.GetDirectoryName(finalPath) ?? Directory.GetCurrentDirectory();
        if (extension.Length > 0 && extension[0] != '.') extension = "." + extension;
        return Path.Combine(directory, $".{Branding.Name}-{Guid.NewGuid():N}{extension}");
    }

    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Branding.Name);
        Directory.CreateDirectory(path);
        return path;
    }

    public static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception) {}
    }

    private static bool IsInside(string target, string source)
    {
        var root = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
