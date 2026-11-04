using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Evals.Decompile;

[Collection(Serial.Name)]
public sealed class DecompileTests
{
    [Fact]
    public async Task DecompileRecoversTheSource()
    {
        Assert.SkipUnless(Tool.Available(() => Tools.CfrJar), "cfr.jar is missing. Run scripts\\fetch.ps1.");
        var javac = FindJdkTool("javac.exe");
        var jar = FindJdkTool("jar.exe");
        Assert.SkipWhen(javac is null || jar is null, "A JDK with javac and jar is needed to build the sample.");
        Assert.SkipUnless(Tool.Available(() => Tools.Java), "Java is not on PATH or JAVA_HOME.");

        TestSettings.Use();
        var folder = Paths.NewFolder("jar");
        var source = Path.Combine(folder, "src");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "Hello.java"),
            "public class Hello { public static void main(String[] args) { System.out.println(\"hi\"); } }",
            TestContext.Current.CancellationToken);
        await Flect.External.ChildProcess.RunOrThrowAsync(javac!, ["-d", source, Path.Combine(source, "Hello.java")], TestContext.Current.CancellationToken);
        var archive = Path.Combine(folder, "hello.jar");
        await Flect.External.ChildProcess.RunOrThrowAsync(jar!, ["cf", archive, "-C", source, "Hello.class"], TestContext.Current.CancellationToken);

        await Rig.RunCleanAsync("jar.decompile", archive);

        var decompiled = Directory.GetFiles(Path.Combine(folder, "hello"), "Hello.java", SearchOption.AllDirectories).Single();
        Assert.Contains("class Hello", await File.ReadAllTextAsync(decompiled, TestContext.Current.CancellationToken));
    }

    private static string? FindJdkTool(string name)
    {
        if (Environment.GetEnvironmentVariable("JAVA_HOME") is { Length: > 0 } home)
        {
            var candidate = Path.Combine(home, "bin", name);
            if (File.Exists(candidate)) return candidate;
        }
        return Tool.FindOnPath(name);
    }
}
