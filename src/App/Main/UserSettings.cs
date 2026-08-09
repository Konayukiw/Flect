namespace Optimizer.Main;

internal sealed class UserSettings
{
    private const string FileName = "settings.json";

    public bool SuppressOcrLanguageNotice { get; set; }

    public static UserSettings Load() => Store.Read<UserSettings>(FileName) ?? new UserSettings();

    public void Save() => Store.Write(FileName, this);
}
