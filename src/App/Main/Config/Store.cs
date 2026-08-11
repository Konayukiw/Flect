using System.Text.Json;
using System.Text.Json.Serialization;

namespace Optimizer.Main;

internal static class Store
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Branding.Name);

    private static readonly JsonSerializerOptions Layout = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Read<T>(string fileName) where T : class
    {
        try
        {
            var path = Path.Combine(Directory, fileName);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Layout)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Write<T>(string fileName, T value)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, fileName),
                              JsonSerializer.Serialize(value, Layout));
        }
        catch (Exception)
        {
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
