using System.Text.Json;

namespace Optimizer.Main;

internal static class Store
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Branding.Name);

    public static T? Read<T>(string fileName) where T : class
    {
        try
        {
            var path = Path.Combine(Directory, fileName);
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null;
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
                              JsonSerializer.Serialize(value));
        }
        catch (Exception)
        {
        }
    }
}
