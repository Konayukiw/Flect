using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Evals.Settings;

[Collection(Serial.Name)]
public sealed class SettingsTests
{
    [Fact]
    public void LocationsFollowFlectHome()
    {
        var home = Path.GetFullPath(Environment.GetEnvironmentVariable("FLECT_HOME")!);
        Assert.Equal(home, Locations.Roaming);
        Assert.Equal(home, Locations.Local);
        Assert.Equal(home, Flect.Settings.SettingsFile.Directory);
    }

    [Fact]
    public void StoreRoundTripsAndSetsCorruptFilesAside()
    {
        var name = $"evals-{Guid.NewGuid():N}.json";
        Flect.Settings.SettingsFile.Write(name, new ArchiveSettings { CompressFormat = ArchiveCompressFormat.TarGz });
        Assert.Equal(ArchiveCompressFormat.TarGz, Flect.Settings.SettingsFile.Read<ArchiveSettings>(name)!.CompressFormat);
        Assert.Empty(Directory.GetFiles(Flect.Settings.SettingsFile.Directory, $".{name}.*.tmp"));

        File.WriteAllText(Path.Combine(Flect.Settings.SettingsFile.Directory, name), "{ broken");
        Assert.Null(Flect.Settings.SettingsFile.Read<ArchiveSettings>(name));
        Assert.True(File.Exists(Path.Combine(Flect.Settings.SettingsFile.Directory, name + ".broken")));
    }

    [Fact]
    public void LegacyDiscordPresetIsRaisedAndMirroredToTheRegistry()
    {
        Assert.SkipUnless(Paths.OnCi, "Writes HKCU\\Software\\Flect, so it runs only on CI.");
        var legacy = new Preferences();
        legacy.Video.Presets.CompressDiscord = VideoPresets.LegacyDiscordLimit;
        var json = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(legacy, new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        }))!.AsObject();
        json.Remove("Version");
        File.WriteAllText(Path.Combine(Flect.Settings.SettingsFile.Directory, "settings.json"), json.ToJsonString());

        try
        {
            var loaded = Preferences.Load();

            Assert.Equal(VideoPresets.DiscordLimit, loaded.Video.Presets.CompressDiscord);
            Assert.Equal(1, Preferences.Load().Version);
            using var presets = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Flect\Presets");
            Assert.Equal(VideoPresets.DiscordLimit.ToString(), presets?.GetValue("Video.CompressDiscord"));
        }
        finally
        {
            File.Delete(Path.Combine(Flect.Settings.SettingsFile.Directory, "settings.json"));
        }
    }
}
