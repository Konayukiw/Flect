namespace Optimizer.Main.Common;

internal static class Locations
{
    private const string HomeVariable = "FLECT_HOME";

    public static string Roaming => Home ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Branding.Name);

    public static string Local => Home ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Branding.Name);

    private static string? Home =>
        Environment.GetEnvironmentVariable(HomeVariable) is { Length: > 0 } home
            ? Path.GetFullPath(home)
            : null;
}
