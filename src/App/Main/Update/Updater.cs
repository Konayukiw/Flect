using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

using Optimizer.Main.Config;

namespace Optimizer.Main.Update;

internal sealed record AssetInfo(string Name, string DownloadUrl, long Size);

internal static class Updater
{
    private const string GitHubApiLatest = "https://api.github.com/repos/Konayukiw/Flect/releases/latest";
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string RunValueName => $"{Branding.Name}AutoUpdate";

    public static void SyncStartup(bool enabled)
    {
        SetStartup(enabled);
    }

    public static void SetStartup(bool enabled)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            var fileName = Path.GetFileName(exePath);
            if (!fileName.StartsWith(Branding.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
            if (key is null) return;

            var valueName = RunValueName;
            if (enabled)
            {
                var command = $"\"{exePath}\" --check-update";
                var current = key.GetValue(valueName) as string;
                if (!string.Equals(current, command, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(valueName, command);
                }
            }
            else
            {
                if (key.GetValue(valueName) is not null)
                {
                    key.DeleteValue(valueName, throwOnMissingValue: false);
                }
            }
        }
        catch
        { /* ignore */ }
    }

    public static async Task RunBackgroundUpdateAsync(bool immediate = false)
    {
        try
        {
            if (!Settings.Current.General.AutoUpdate) return;

            if (!immediate && !Debugger.IsAttached)
            {
                await Task.Delay(5000);
            }

            string? json = null;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Flect-Updater");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var response = await client.GetAsync(GitHubApiLatest);
                        if (response.IsSuccessStatusCode)
                        {
                            json = await response.Content.ReadAsStringAsync();
                            break;
                        }
                    }
                    catch
                    {
                        if (attempt == 2) return;
                        await Task.Delay(5000);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(json)) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return;

            var tagName = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
            var releaseName = root.TryGetProperty("name", out var relName) ? relName.GetString() : null;

            if (!TryParseVersion(tagName, out var remoteVersion) &&
                !TryParseVersion(releaseName, out remoteVersion))
            {
                return;
            }

            if (!TryParseVersion(Branding.Version, out var currentVersion))
            {
                return;
            }

            if (remoteVersion <= currentVersion)
            {
                return;
            }

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var setupAsset = FindSetupAsset(assets);
            if (setupAsset is null) return;

            var tempDir = Path.Combine(Path.GetTempPath(), "FlectUpdate");
            Directory.CreateDirectory(tempDir);
            var setupPath = Path.Combine(tempDir, setupAsset.Name);

            using (var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("Flect-Updater");
                using var response = await downloadClient.GetAsync(setupAsset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var file = new FileStream(setupPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(file);
            }

            if (!File.Exists(setupPath)) return;

            var scriptPath = Path.Combine(tempDir, "apply-update.cmd");
            var scriptContent =
                $"@echo off\r\n" +
                $"timeout /t 3 /nobreak >nul 2>&1 || ping 127.0.0.1 -n 4 >nul\r\n" +
                $"\"{setupPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-\r\n" +
                $"del \"%~f0\"\r\n";

            await File.WriteAllTextAsync(scriptPath, scriptContent, Encoding.ASCII);

            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            Environment.Exit(0);
        }
        catch
        { /* ignore */ }
    }

    internal static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = Regex.Match(text, @"(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (!match.Success) return false;

        int major = int.Parse(match.Groups[1].Value);
        int minor = int.Parse(match.Groups[2].Value);
        int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        int revision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

        version = new Version(major, minor, build, revision);
        return true;
    }

    internal static AssetInfo? FindSetupAsset(JsonElement assets)
    {
        if (assets.ValueKind != JsonValueKind.Array) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            long size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) continue;

            if (Regex.IsMatch(name, @"^Flect-.*setup\.exe$", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(name, @"(?i)setup.*\.exe$"))
            {
                return new AssetInfo(name, url, size);
            }
        }

        return null;
    }
}
