using Evals.Infrastructure;

namespace Evals;

[Collection(Serial.Name)]
public sealed class OutputTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmptySettingKeepsTheOutputBesideTheSource()
    {
        var source = Paths.NewFolder("output-default-source");
        var file = Path.Combine(source, "data.json");
        await File.WriteAllTextAsync(file, "{\"b\":1,\"a\":2}", Token);
        TestSettings.Use();

        await Harness.RunCleanAsync("json.pretty", file);

        Assert.True(File.Exists(Path.Combine(source, "data_pretty.json")));
    }

    [Fact]
    public async Task AbsoluteOutputFolderReceivesTheResult()
    {
        var source = Paths.NewFolder("output-absolute-source");
        var target = Paths.NewFolder("output-absolute-target");
        var file = Path.Combine(source, "data.json");
        await File.WriteAllTextAsync(file, "{\"b\":1,\"a\":2}", Token);
        TestSettings.Use(settings => settings.Output.Folders["json.pretty"] = target);

        await Harness.RunCleanAsync("json.pretty", file);

        Assert.True(File.Exists(Path.Combine(target, "data_pretty.json")));
        Assert.False(File.Exists(Path.Combine(source, "data_pretty.json")));
    }

    [Fact]
    public async Task RelativeOutputFolderStartsFromTheSourceFolder()
    {
        var source = Paths.NewFolder("output-relative-source");
        var file = Path.Combine(source, "data.json");
        await File.WriteAllTextAsync(file, "{\"b\":1,\"a\":2}", Token);
        TestSettings.Use(settings => settings.Output.Folders["json.pretty"] = "./pretty");

        await Harness.RunCleanAsync("json.pretty", file);

        Assert.True(File.Exists(Path.Combine(source, "pretty", "data_pretty.json")));
        Assert.False(File.Exists(Path.Combine(source, "data_pretty.json")));
    }

    [Fact]
    public void DirectoryForExpandsEnvironmentVariables()
    {
        var source = Paths.NewFolder("output-env-source");
        var target = Paths.NewFolder("output-env-target");
        Environment.SetEnvironmentVariable("FLECT_OUTPUT_TEST", target);
        try
        {
            TestSettings.Use(settings => settings.Output.Folders["json.pretty"] = "%FLECT_OUTPUT_TEST%");
            OutputPath.TaskId = "json.pretty";

            var directory = OutputPath.DirectoryFor(Path.Combine(source, "data.json"));

            Assert.Equal(target, directory, ignoreCase: true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLECT_OUTPUT_TEST", null);
            OutputPath.TaskId = null;
        }
    }

    [Fact]
    public void DirectoryForRejectsAnOutputFolderInsideTheSourceFolder()
    {
        var source = Paths.NewFolder("output-guard-source");
        TestSettings.Use(settings =>
            settings.Output.Folders["folder.compress"] = Path.Combine(source, "archives"));
        OutputPath.TaskId = "folder.compress";
        try
        {
            Assert.Throws<InvalidOperationException>(() => OutputPath.DirectoryFor(source));
        }
        finally
        {
            OutputPath.TaskId = null;
        }
    }
}
