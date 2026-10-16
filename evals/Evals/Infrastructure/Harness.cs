using System.Reflection;
using System.Text;

namespace Evals.Infrastructure;

internal sealed class RecordingProgress(CancellationToken token = default) : ITaskProgress
{
    private readonly Lock _gate = new();

    public List<string> Infos { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public CancellationToken Token => token;

    public void Status(string text)
    {
    }

    public void Step(int done, int total)
    {
    }

    public void Indeterminate()
    {
    }

    public void Info(string message)
    {
        lock (_gate) Infos.Add(message);
    }

    public void Warn(string message)
    {
        lock (_gate) Warnings.Add(message);
    }

    public void Error(string message)
    {
        lock (_gate) Errors.Add(message);
    }

    public void AssertClean() =>
        Assert.True(Errors.Count == 0, "The task reported errors: " + string.Join(" | ", Errors));
}

internal static class Harness
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    public static Request Request(string verb, params string[] paths)
    {
        var selection = Path.Combine(Paths.Work, $"selection-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(selection, paths, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return Optimizer.Main.Process.Request.Parse(["--task", verb, "--input", selection])
            ?? throw new InvalidOperationException($"The verb '{verb}' could not be parsed.");
    }

    public static TaskBase Create(string verb, params string[] paths) =>
        Factory.Create(Request(verb, paths)) ?? throw new InvalidOperationException($"No task handles '{verb}'.");

    public static async Task<RecordingProgress> RunAsync(TaskBase task, bool configure = true,
                                                         CancellationToken token = default)
    {
        if (configure) Assert.True(task.Configure(), $"{task.GetType().Name}.Configure returned false.");
        var progress = new RecordingProgress(token);
        await task.RunAsync(progress);
        return progress;
    }

    public static Task<RecordingProgress> RunAsync(string verb, params string[] paths) =>
        RunAsync(Create(verb, paths));

    public static async Task<RecordingProgress> RunCleanAsync(string verb, params string[] paths)
    {
        var progress = await RunAsync(verb, paths);
        progress.AssertClean();
        return progress;
    }

    public static void Set(object target, string field, object? value) =>
        FieldOf(target, field).SetValue(target, value);

    public static T Get<T>(object target, string field) => (T)FieldOf(target, field).GetValue(target)!;

    private static FieldInfo FieldOf(object target, string field) =>
        target.GetType().GetField(field, Hidden)
            ?? throw new MissingFieldException(target.GetType().Name, field);
}

internal static class TestSettings
{
    private static readonly FieldInfo Current =
        typeof(Settings).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(nameof(Settings), "_current");

    public static Settings Use(Action<Settings>? configure = null)
    {
        var settings = new Settings { Version = 1 };
        settings.Ocr.SuppressLanguageNotice = true;
        settings.Folder.DeleteMethod = DeleteMethod.Permanent;
        settings.Folder.Duplicates.Keep = DuplicateKeep.Oldest;
        settings.Video.Priority = EncodePriority.Speed;
        configure?.Invoke(settings);
        Current.SetValue(null, settings);
        return settings;
    }
}
