namespace Flect.Settings;

internal sealed class Settings
{
    private const string FileName = "settings.json";
    private const int SchemaVersion = 1;

    private static Settings? _current;

    public int Version { get; set; }

    public GeneralSettings General { get; set; } = new();
    public FolderSettings Folder { get; set; } = new();
    public VideoSettings Video { get; set; } = new();
    public ImageSettings Image { get; set; } = new();
    public TextSettings Text { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public CodeSettings Code { get; set; } = new();
    public OutputSettings Output { get; set; } = new();

    public static Settings Current => _current ??= Load();

    public static Settings Load()
    {
        var settings = SettingsFile.Read<Settings>(FileName);
        if (settings is null) return new Settings();
        if (settings.Version >= SchemaVersion) return settings;

        settings.Upgrade();
        settings.Version = SchemaVersion;
        SettingsFile.Write(FileName, settings);
        ShellRegistry.Publish(settings);
        return settings;
    }

    private void Upgrade()
    {
        if (Video.Presets.CompressDiscord == VideoPresets.LegacyDiscordLimit)
        {
            Video.Presets.CompressDiscord = VideoPresets.DiscordLimit;
        }
    }

    public void Save()
    {
        Version = SchemaVersion;
        SettingsFile.Write(FileName, this);
        ShellRegistry.Publish(this);
        _current = this;
    }

    public static void Reset()
    {
        SettingsFile.Delete(FileName);
        ShellRegistry.Clear();
        _current = new Settings();
    }
}
