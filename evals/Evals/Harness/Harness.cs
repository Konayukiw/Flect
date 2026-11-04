using System.Reflection;
using System.Text;

namespace Evals.Harness;

internal static class Harness
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    public static Request Request(string verb, params string[] paths)
    {
        var selection = Path.Combine(Paths.Work, $"selection-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(selection, paths, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return Flect.Dispatch.Request.Parse(["--task", verb, "--input", selection])
            ?? throw new InvalidOperationException($"The verb '{verb}' could not be parsed.");
    }

    public static TaskBase Create(string verb, params string[] paths) =>
        Flect.Dispatch.Dispatch.Create(Request(verb, paths)) ?? throw new InvalidOperationException($"No task handles '{verb}'.");

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
