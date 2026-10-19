using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Evals.Infrastructure;
using Microsoft.Win32;

namespace Evals;

[Collection(Serial.Name)]
public sealed class AutoUpdateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly byte[] Setup = "test installer, never executed"u8.ToArray();

    [Theory]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("V2.0.0", "1.9.9", true)]
    [InlineData("1.10.0", "1.9.0", true)]
    [InlineData("1.0.0.1", "1.0.0", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("1.0.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0.0", false)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("v2.0.0-beta.1", "1.0.0", false)]
    [InlineData("latest", "1.0.0", false)]
    [InlineData("2.0.0", "invalid", false)]
    public void ComparesReleaseVersionsNumerically(string tag, string current, bool expected) =>
        Assert.Equal(expected, AutoUpdate.IsNewer(tag, current));

    [Fact]
    public void AutoUpdateIsOptInAndSurvivesSerialization()
    {
        var legacy = JsonSerializer.Deserialize<Settings>("{\"General\":{\"CloseOnSuccess\":true}}");
        Assert.False(legacy!.General.AutoUpdate);
        legacy.General.AutoUpdate = true;
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(legacy));
        Assert.True(restored!.General.AutoUpdate);
    }

    [Fact]
    public void StartupRegistrationIsQuotedAndRemovedWhenDisabled()
    {
        var path = @"Software\Flect-Evals\" + Guid.NewGuid().ToString("N");
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(path);
            run.SetValue("Unrelated", "keep");
            const string executable = @"C:\Path with spaces\Flect.exe";
            AutoUpdate.ConfigureStartup(run, true, executable);
            Assert.Equal($"\"{executable}\" --auto-update", run.GetValue(Branding.Name + "AutoUpdate"));
            AutoUpdate.ConfigureStartup(run, false, executable);
            AutoUpdate.ConfigureStartup(run, false, executable);
            Assert.Null(run.GetValue(Branding.Name + "AutoUpdate"));
            Assert.Equal("keep", run.GetValue("Unrelated"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
    }

    [Theory]
    [InlineData("Flect-2.0.0-setup.exe")]
    [InlineData("Flect-Setup.exe")]
    public async Task DownloadsVerifiesAndLaunchesSetupSilently(string name)
    {
        var release = Release();
        release["assets"]![0]!["name"] = name;
        using var server = new GitHub(release);
        using var client = new HttpClient(server);
        var folder = Paths.NewFolder("auto-update");
        ProcessStartInfo? launched = null;

        await AutoUpdate.CheckAndInstallAsync(client, "1.0.0", folder, start => launched = start, Token);

        Assert.NotNull(launched);
        Assert.Equal(2, server.Requests);
        Assert.Equal(Setup, await File.ReadAllBytesAsync(launched.FileName, Token));
        Assert.False(launched.UseShellExecute);
        Assert.True(launched.CreateNoWindow);
        Assert.Contains("/VERYSILENT", launched.ArgumentList);
        Assert.Contains("/SUPPRESSMSGBOXES", launched.ArgumentList);
        Assert.Contains("/NORESTART", launched.ArgumentList);
        Assert.Contains("/SP-", launched.ArgumentList);
        Assert.Contains("/DIR=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), launched.ArgumentList);
        Assert.Contains(launched.ArgumentList, argument => argument.StartsWith("/LOG="));
    }

    [Theory]
    [InlineData("tag_name", "v1.0.0")]
    [InlineData("tag_name", "v0.9.0")]
    [InlineData("tag_name", "v2.0.0-beta")]
    [InlineData("tag_name", "invalid")]
    [InlineData("draft", "true")]
    [InlineData("prerelease", "true")]
    public async Task IgnoresCurrentOlderAndNonStableReleases(string property, string value)
    {
        var release = Release();
        release[property] = property == "tag_name" ? JsonValue.Create(value) : JsonValue.Create(true);
        await AssertIgnored(release);
    }

    [Theory]
    [InlineData("name", "Flect-2.0.0.zip")]
    [InlineData("name", "Other-Setup.exe")]
    [InlineData("browser_download_url", "http://github.com/Konayukiw/Flect/releases/download/v2.0.0/Flect-Setup.exe")]
    [InlineData("browser_download_url", "https://example.com/Flect-Setup.exe")]
    [InlineData("browser_download_url", "https://github.com/Other/Repo/releases/download/v2.0.0/Flect-Setup.exe")]
    [InlineData("digest", null)]
    [InlineData("digest", "sha256:invalid")]
    [InlineData("digest", "sha256:zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task IgnoresMissingOrUnverifiableInstallers(string property, string? value)
    {
        var release = Release();
        release["assets"]![0]![property] = value;
        await AssertIgnored(release);
    }

    [Fact]
    public async Task IgnoresReleaseWithoutAssets()
    {
        var release = Release();
        release["assets"] = null;
        await AssertIgnored(release);
    }

    [Fact]
    public async Task RejectsHashMismatchAndDeletesDownload()
    {
        var release = Release();
        release["assets"]![0]!["digest"] = "sha256:" + new string('0', 64);
        using var server = new GitHub(release);
        using var client = new HttpClient(server);
        var folder = Paths.NewFolder("auto-update-hash");
        await AutoUpdate.CheckAndInstallAsync(client, "1.0.0", folder, _ => Assert.Fail("Must not launch"), Token);
        Assert.Equal(2, server.Requests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task HttpErrorsNeverLaunchSetup(HttpStatusCode status)
    {
        using var server = new GitHub(Release()) { Status = status };
        using var client = new HttpClient(server);
        await Assert.ThrowsAsync<HttpRequestException>(() => AutoUpdate.CheckAndInstallAsync(client,
            "1.0.0", Paths.NewFolder("auto-update-http"), _ => Assert.Fail("Must not launch"), Token));
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task CleansUpWhenDownloadFails()
    {
        using var server = new GitHub(Release()) { DownloadStatus = HttpStatusCode.NotFound };
        using var client = new HttpClient(server);
        var folder = Paths.NewFolder("auto-update-download");
        await Assert.ThrowsAsync<HttpRequestException>(() => AutoUpdate.CheckAndInstallAsync(client,
            "1.0.0", folder, _ => Assert.Fail("Must not launch"), Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Fact]
    public async Task CancellationNeverLaunchesSetup()
    {
        using var server = new GitHub(Release());
        using var client = new HttpClient(server);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AutoUpdate.CheckAndInstallAsync(client,
            "1.0.0", Paths.NewFolder("auto-update-cancel"), _ => Assert.Fail("Must not launch"), cancelled.Token));
    }

    private static async Task AssertIgnored(JsonObject release)
    {
        using var server = new GitHub(release);
        using var client = new HttpClient(server);
        var folder = Paths.NewFolder("auto-update-skip");
        await AutoUpdate.CheckAndInstallAsync(client, "1.0.0", folder, _ => Assert.Fail("Must not launch"), Token);
        Assert.Equal(1, server.Requests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Fact]
    public async Task CleansOldDownloadsEvenWhenAlreadyUpToDate()
    {
        using var server = new GitHub(Release());
        using var client = new HttpClient(server);
        var folder = Paths.NewFolder("auto-update-cleanup");
        var stale = Directory.CreateDirectory(Path.Combine(folder, "old")).FullName;
        await File.WriteAllTextAsync(Path.Combine(stale, "setup.exe"), "old", Token);
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));
        var recent = Directory.CreateDirectory(Path.Combine(folder, "recent")).FullName;
        await AutoUpdate.CheckAndInstallAsync(client, "2.0.0", folder, _ => Assert.Fail("Must not launch"), Token);
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(recent));
    }

    private static JsonObject Release() => new()
    {
        ["tag_name"] = "v2.0.0",
        ["draft"] = false,
        ["prerelease"] = false,
        ["assets"] = new JsonArray(new JsonObject
        {
            ["name"] = "Flect-2.0.0-setup.exe",
            ["browser_download_url"] = "https://github.com/Konayukiw/Flect/releases/download/v2.0.0/Flect-2.0.0-setup.exe",
            ["digest"] = "sha256:" + Convert.ToHexString(SHA256.HashData(Setup)),
        }),
    };

    private sealed class GitHub(JsonObject release) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode DownloadStatus { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Requests++;
            if (Requests == 1)
            {
                Assert.Equal("https://api.github.com/repos/Konayukiw/Flect/releases/latest", request.RequestUri!.AbsoluteUri);
                Assert.Contains(Branding.Name, request.Headers.UserAgent.ToString());
                Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
                return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(release.ToJsonString()) });
            }
            Assert.Equal(release["assets"]![0]!["browser_download_url"]!.GetValue<string>(), request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(DownloadStatus) { Content = new ByteArrayContent(Setup) });
        }
    }
}
