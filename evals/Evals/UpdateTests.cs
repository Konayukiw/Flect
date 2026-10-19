using System.Text.Json;
using Optimizer.Main.Update;

namespace Evals;

public sealed class UpdateTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0, 0)]
    [InlineData("flect-1.0", 1, 0, 0, 0)]
    [InlineData("v1.0.1", 1, 0, 1, 0)]
    [InlineData("Flect 2.5", 2, 5, 0, 0)]
    [InlineData("3.14.15.92", 3, 14, 15, 92)]
    public void ParsesVersionNumbersAccurately(string input, int major, int minor, int build, int revision)
    {
        Assert.True(Updater.TryParseVersion(input, out var version));
        Assert.Equal(new Version(major, minor, build, revision), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("none")]
    [InlineData(null)]
    public void FailsGracefullyOnInvalidVersionStrings(string? input)
    {
        Assert.False(Updater.TryParseVersion(input, out _));
    }

    [Fact]
    public void VersionComparisonCorrectlyIdentifiesNewerVersions()
    {
        Assert.True(Updater.TryParseVersion("1.0.0", out var current));
        Assert.True(Updater.TryParseVersion("flect-1.0", out var same));
        Assert.True(Updater.TryParseVersion("v1.0.1", out var patch));
        Assert.True(Updater.TryParseVersion("flect-1.1", out var minor));
        Assert.True(Updater.TryParseVersion("2.0.0", out var major));

        Assert.Equal(0, same.CompareTo(current));
        Assert.True(patch.CompareTo(current) > 0);
        Assert.True(minor.CompareTo(current) > 0);
        Assert.True(major.CompareTo(current) > 0);
        Assert.False(current.CompareTo(patch) > 0);
    }

    [Fact]
    public void FindsMatchingSetupAssetInReleaseJson()
    {
        var json = """
        [
            {
                "name": "source.zip",
                "browser_download_url": "https://example.com/source.zip",
                "size": 1000
            },
            {
                "name": "Flect-Setup.exe",
                "browser_download_url": "https://github.com/Konayukiw/Flect/releases/download/flect-1.0/Flect-Setup.exe",
                "size": 83885684
            }
        ]
        """;

        using var doc = JsonDocument.Parse(json);
        var asset = Updater.FindSetupAsset(doc.RootElement);

        Assert.NotNull(asset);
        Assert.Equal("Flect-Setup.exe", asset.Name);
        Assert.Equal("https://github.com/Konayukiw/Flect/releases/download/flect-1.0/Flect-Setup.exe", asset.DownloadUrl);
        Assert.Equal(83885684, asset.Size);
    }

    [Fact]
    public void ReturnsNullWhenNoSetupAssetMatches()
    {
        var json = """
        [
            {
                "name": "readme.txt",
                "browser_download_url": "https://example.com/readme.txt",
                "size": 500
            }
        ]
        """;

        using var doc = JsonDocument.Parse(json);
        var asset = Updater.FindSetupAsset(doc.RootElement);

        Assert.Null(asset);
    }
}
