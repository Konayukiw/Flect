using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Optimizer.Main.Config;

internal static class Store
{
    private const int Attempts = 5;
    private const int BackoffMs = 50;
    private const string BrokenSuffix = ".broken";

    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    public static string Directory => Locations.Roaming;

    private static readonly JsonSerializerOptions Layout = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Read<T>(string fileName) where T : class
    {
        var path = Path.Combine(Directory, fileName);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<T>(ReadText(path), Layout);
            }
            catch (JsonException) when (attempt == 1)
            {
                Thread.Sleep(BackoffMs);
            }
            catch (JsonException)
            {
                SetAside(path);
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static void Write<T>(string fileName, T value)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = Target(Path.Combine(Directory, fileName));
            var folder = Path.GetDirectoryName(path) ?? Directory;
            var name = Path.GetFileName(path);
            SweepStale(folder, name);

            var temp = Path.Combine(folder, $".{name}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, value, Layout);
                    stream.Flush(flushToDisk: true);
                }
                Replace(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        catch (Exception)
        {
        }
    }

    private static string ReadText(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                  FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8,
                                                    detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < Attempts)
            {
                Thread.Sleep(BackoffMs * attempt);
            }
        }
    }

    private static void Replace(string temp, string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException &&
                                       attempt < Attempts)
            {
                Thread.Sleep(BackoffMs * attempt);
            }
        }
    }

    private static string Target(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static void SetAside(string path)
    {
        try
        {
            File.Move(path, path + BrokenSuffix, overwrite: true);
        }
        catch (Exception)
        {
        }
    }

    private static void SweepStale(string folder, string name)
    {
        var cutoff = DateTime.UtcNow - StaleAfter;
        foreach (var stale in System.IO.Directory.EnumerateFiles(folder, $".{name}.*.tmp"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(stale) < cutoff) File.Delete(stale);
            }
            catch (Exception)
            {
            }
        }
    }

    public static void Delete(string fileName)
    {
        try
        {
            var path = Path.Combine(Directory, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
        }
    }
}
