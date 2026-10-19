using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Optimizer.Main.Config;

internal static class AutoUpdate
{
    public const string StartupArgument = "--auto-update";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Repository = "Konayukiw/Flect";
    private static string StartupName => Branding.Name + "AutoUpdate";

    public static void ConfigureStartup(bool enabled)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            ConfigureStartup(run, enabled, Path.Combine(AppContext.BaseDirectory, Branding.Name + ".exe"));
        }
        catch (Exception)
        {
        }
    }

    internal static void ConfigureStartup(RegistryKey run, bool enabled, string executable)
    {
        if (enabled)
            run.SetValue(StartupName, $"\"{executable}\" {StartupArgument}", RegistryValueKind.String);
        else
            run.DeleteValue(StartupName, throwOnMissingValue: false);
    }

    public static async Task RunAsync()
    {
        if (!Settings.Current.General.AutoUpdate) return;

        try
        {
            using var singleInstance = new Mutex(false, @"Local\" + StartupName, out var created);
            if (!created) return;

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await CheckAndInstallAsync(client, Branding.Version,
                Path.Combine(Path.GetTempPath(), Branding.Name + "-updates"),
                start => System.Diagnostics.Process.Start(start)?.Dispose(), timeout.Token);
        }
        catch (Exception)
        {
            // Startup checks must never interrupt sign-in, including when offline.
        }
    }

    internal static async Task CheckAndInstallAsync(HttpClient client, string currentVersion,
        string downloadDirectory, Action<ProcessStartInfo> launch, CancellationToken token)
    {
        if (Directory.Exists(downloadDirectory))
        {
            foreach (var previous in Directory.EnumerateDirectories(downloadDirectory))
            {
                if (Directory.GetLastWriteTimeUtc(previous) >= DateTime.UtcNow.AddDays(-1)) continue;
                try { Directory.Delete(previous, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd($"{Branding.Name}/{Branding.Version}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<Release>(token);
        if (release is null || release.Draft || release.Prerelease ||
            !IsNewer(release.Tag, currentVersion)) return;

        var asset = release.Assets?.FirstOrDefault(asset =>
            (string.Equals(asset.Name, $"{Branding.Name}-{release.Tag.TrimStart('v', 'V')}-setup.exe",
                StringComparison.OrdinalIgnoreCase) ||
             string.Equals(asset.Name, Branding.Name + "-Setup.exe", StringComparison.OrdinalIgnoreCase)) &&
            Uri.TryCreate(asset.Url, UriKind.Absolute, out var url) &&
            url.Scheme == Uri.UriSchemeHttps && url.Host == "github.com" &&
            url.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal));
        if (asset is null || !TryDigest(asset.Digest, out var expected)) return;

        Directory.CreateDirectory(downloadDirectory);
        var work = Path.Combine(downloadDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var launched = false;
        try
        {
            var setup = Path.Combine(work, "setup.exe");
            using (var download = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                download.EnsureSuccessStatusCode();
                await using var file = new FileStream(setup, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                await download.Content.CopyToAsync(file, token);
                file.Position = 0;
                var actual = await SHA256.HashDataAsync(file, token);
                if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return;
            }

            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo(setup) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("/VERYSILENT");
            start.ArgumentList.Add("/SUPPRESSMSGBOXES");
            start.ArgumentList.Add("/NORESTART");
            start.ArgumentList.Add("/SP-");
            start.ArgumentList.Add("/DIR=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            start.ArgumentList.Add("/LOG=" + Path.Combine(work, "setup.log"));
            launch(start);
            // Setup stops the worker itself; leave the download until the next startup check.
            launched = true;
        }
        finally
        {
            if (!launched) Directory.Delete(work, recursive: true);
        }
    }

    internal static bool IsNewer(string tag, string current)
    {
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) ||
            !Version.TryParse(current, out var installed)) return false;
        return Normalize(latest) > Normalize(installed);
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));

    private static bool TryDigest(string? digest, out byte[] hash)
    {
        hash = [];
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
            digest.Length != 71) return false;
        try
        {
            hash = Convert.FromHexString(digest[7..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; init; } = string.Empty;
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("assets")] public Asset[]? Assets { get; init; }
    }

    private sealed class Asset
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string Url { get; init; } = string.Empty;
        [JsonPropertyName("digest")] public string? Digest { get; init; }
    }
}
