namespace Optimizer.Main;

internal sealed class Settings
{
    private const string FileName = "settings.json";

    public bool SuppressOcrLanguageNotice { get; set; }

    public static Settings Load() => Store.Read<Settings>(FileName) ?? new Settings();

    public void Save() => Store.Write(FileName, this);
}
