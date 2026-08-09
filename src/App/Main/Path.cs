namespace Optimizer.Main;

internal static class OutputPath
{
    public static string Derive(string source, string suffix = "", string? extension = null)
    {
        var directory = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
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

    public static string DeriveDirectory(string source, string suffix = "")
    {
        var directory = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        var stem = Path.GetFileNameWithoutExtension(source) + suffix;

        var candidate = Path.Combine(directory, stem);
        for (int n = 2; Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{stem} ({n})");
        }
        return candidate;
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

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
