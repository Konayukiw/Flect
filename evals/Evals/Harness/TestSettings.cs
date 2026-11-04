using System.Reflection;
using System.Text;

namespace Evals.Harness;

internal static class TestSettings
{
    private static readonly FieldInfo Current =
        typeof(Preferences).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(nameof(Preferences), "_current");

    public static Preferences Use(Action<Preferences>? configure = null)
    {
        var settings = new Preferences { Version = 1 };
        settings.Ocr.SuppressLanguageNotice = true;
        settings.Folder.DeleteMethod = DeleteMethod.Permanent;
        settings.Folder.Duplicates.Keep = DuplicateKeep.Oldest;
        settings.Video.Priority = EncodePriority.Speed;
        configure?.Invoke(settings);
        Current.SetValue(null, settings);
        return settings;
    }
}
