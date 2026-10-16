using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Evals.Infrastructure;
using UglyToad.PdfPig;

namespace Evals;

[Collection(Serial.Name)]
public sealed class PdfTests
{
    [Theory]
    [InlineData("PNG", ".png")]
    [InlineData("JPG", ".jpg")]
    public async Task ConvertRendersEveryPage(string target, string extension)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("pdf-render");
        var input = await Fixtures.CopyAsync("alpha.pdf", folder);

        await Harness.RunCleanAsync($"pdf.convert?to={target}", input);

        Assert.True(File.Exists(Path.Combine(folder, "alpha_p1" + extension)));
        Assert.True(File.Exists(Path.Combine(folder, "alpha_p2" + extension)));
    }

    [Fact]
    public async Task ConvertToTextExtractsTheWords()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("pdf-text");
        var input = await Fixtures.CopyAsync("alpha.pdf", folder);

        await Harness.RunCleanAsync("pdf.convert?to=TXT", input);

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "alpha.txt"), TestContext.Current.CancellationToken);
        Assert.Contains("alpha page 1", text);
        Assert.Contains("alpha page 2", text);
    }

    [Fact]
    public async Task MergeFollowsTheChosenOrder()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("pdf-merge");
        var alpha = await Fixtures.CopyAsync("alpha.pdf", folder);
        var bravo = await Fixtures.CopyAsync("bravo.pdf", folder);
        var task = Harness.Create("pdf.merge", alpha, bravo);
        Harness.Set(task, "_sources", new[] { bravo, alpha });

        var progress = await Harness.RunAsync(task, configure: false, token: TestContext.Current.CancellationToken);

        progress.AssertClean();
        using var merged = PdfDocument.Open(Path.Combine(folder, "bravo_merged.pdf"));
        Assert.Equal(4, merged.NumberOfPages);
        Assert.Contains("bravo page 1", merged.GetPage(1).Text);
        Assert.Contains("alpha page 1", merged.GetPage(3).Text);
    }
}

[Collection(Serial.Name)]
public sealed class TextTests
{
    private const string Japanese = "日本語のテキストです。\r\n二行目";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> WriteAsync(string folder, string name, string text, Encoding? encoding = null)
    {
        var path = Path.Combine(folder, name);
        await File.WriteAllTextAsync(path, text, encoding ?? new UTF8Encoding(false), Token);
        return path;
    }

    [Theory]
    [InlineData("UTF8BOM", "jp_utf8bom.txt", new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData("UTF16", "jp_utf16.txt", new byte[] { 0xFF, 0xFE })]
    public async Task EncodeAddsTheRightByteOrderMark(string target, string output, byte[] mark)
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-encode");
        var input = await WriteAsync(folder, "jp.txt", Japanese);

        await Harness.RunCleanAsync($"text.encode?to={target}", input);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, output), Token);
        Assert.Equal(mark, bytes[..mark.Length]);
    }

    [Fact]
    public async Task EncodeToShiftJisRoundTrips()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-sjis");
        var input = await WriteAsync(folder, "jp.txt", Japanese);

        await Harness.RunCleanAsync("text.encode?to=SJIS", input);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, "jp_sjis.txt"), Token);
        Assert.Equal(Japanese, Encoding.GetEncoding(932).GetString(bytes));
    }

    [Fact]
    public async Task ShiftJisWarnsAboutCharactersItCannotHold()
    {
        TestSettings.Use(settings => settings.Text.SjisFallback = SjisFallback.ReplaceAndWarn);
        var folder = Paths.NewFolder("text-sjis-warn");
        var input = await WriteAsync(folder, "emoji.txt", "絵文字🫪");

        var progress = await Harness.RunCleanAsync("text.encode?to=SJIS", input);

        Assert.True(File.Exists(Path.Combine(folder, "emoji_sjis.txt")));
        Assert.NotEmpty(progress.Warnings);
    }

    [Fact]
    public async Task ShiftJisCanRefuseInsteadOfReplacing()
    {
        TestSettings.Use(settings => settings.Text.SjisFallback = SjisFallback.Fail);
        var folder = Paths.NewFolder("text-sjis-fail");
        var input = await WriteAsync(folder, "emoji.txt", "絵文字😀");

        var progress = await Harness.RunAsync("text.encode?to=SJIS", input);

        Assert.NotEmpty(progress.Errors);
        Assert.False(File.Exists(Path.Combine(folder, "emoji_sjis.txt")));
    }

    [Fact]
    public async Task LineEndingsBecomeLf()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-lf");
        var input = await WriteAsync(folder, "script.sh", "#!/bin/sh\r\necho hello\r\n");

        await Harness.RunCleanAsync("text.lineEndings?to=LF", input);

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "script_lf.sh"), Token);
        Assert.Equal("#!/bin/sh\necho hello\n", text);
    }

    [Fact]
    public async Task LineEndingsBecomeCrlf()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-crlf");
        var input = await WriteAsync(folder, "notes.txt", "one\ntwo\n");

        await Harness.RunCleanAsync("text.lineEndings?to=CRLF", input);

        Assert.Equal("one\r\ntwo\r\n", await File.ReadAllTextAsync(Path.Combine(folder, "notes_crlf.txt"), Token));
    }

    [Fact(Skip = "Known bug: converting line endings writes a UTF-8 BOM into files that had none.")]
    public async Task LineEndingsDoNotAddAByteOrderMark()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-bom");
        var input = await WriteAsync(folder, "script.sh", "#!/bin/sh\r\necho 日本語\r\n");

        await Harness.RunCleanAsync("text.lineEndings?to=LF", input);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, "script_lf.sh"), Token);
        Assert.Equal((byte)'#', bytes[0]);
    }

    [Fact]
    public async Task CsvBecomesEveryTableFormat()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-csv");
        var input = await WriteAsync(folder, "people.csv", "name,age\nAlice,30\nBob,25\n");

        foreach (var target in new[] { "JSON", "XML", "HTML", "MD", "TXT" })
        {
            await Harness.RunCleanAsync($"text.convert?to={target}", input);
        }

        var json = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "people.json"), Token))!.AsArray();
        Assert.Equal("Alice", json[0]!["name"]!.GetValue<string>());
        Assert.Contains("Bob", XDocument.Load(Path.Combine(folder, "people.xml")).ToString());
        Assert.Contains("<td>Alice</td>", await File.ReadAllTextAsync(Path.Combine(folder, "people.html"), Token));
        Assert.Contains("| Alice |", await File.ReadAllTextAsync(Path.Combine(folder, "people.md"), Token));
        Assert.Contains("Alice,30", await File.ReadAllTextAsync(Path.Combine(folder, "people.txt"), Token));
    }

    [Fact]
    public async Task JsonBecomesCsvAndXml()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-json");
        var input = await WriteAsync(folder, "rows.json", "[{\"id\":1,\"label\":\"first\"},{\"id\":2,\"label\":\"second\"}]");

        await Harness.RunCleanAsync("text.convert?to=CSV", input);
        await Harness.RunCleanAsync("text.convert?to=XML", input);

        var csv = await File.ReadAllTextAsync(Path.Combine(folder, "rows.csv"), Token);
        Assert.StartsWith("id,label", csv.TrimStart('﻿'));
        Assert.Contains("second", XDocument.Load(Path.Combine(folder, "rows.xml")).ToString());
    }

    [Fact]
    public async Task XmlAndIniBecomeJson()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("text-xml-ini");
        var xml = await WriteAsync(folder, "config.xml", "<config><name>Flect</name><mode>fast</mode></config>");
        var ini = await WriteAsync(folder, "app.ini", "[window]\nwidth=800\nheight=600\n");

        await Harness.RunCleanAsync("text.convert?to=JSON", xml);
        await Harness.RunCleanAsync("text.convert?to=JSON", ini);
        await Harness.RunCleanAsync("text.convert?to=XML", ini);

        Assert.Contains("Flect", await File.ReadAllTextAsync(Path.Combine(folder, "config.json"), Token));
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "app.json"), Token))!;
        Assert.Equal("800", settings["window"]!["width"]!.ToString());
        Assert.Contains("600", XDocument.Load(Path.Combine(folder, "app.xml")).ToString());
    }

    [Fact]
    public async Task JsonPrettyPrintIndents()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("json-pretty");
        var input = await WriteAsync(folder, "data.json", "{\"b\":1,\"a\":[1,2]}");

        await Harness.RunCleanAsync("json.pretty", input);

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "data_pretty.json"), Token);
        Assert.Contains("\n", text);
        Assert.True(text.IndexOf("\"b\"", StringComparison.Ordinal) < text.IndexOf("\"a\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JsonSortOrdersKeysRecursively()
    {
        TestSettings.Use(settings => settings.Text.JsonSort.Comparison = JsonKeyOrder.Natural);
        var folder = Paths.NewFolder("json-sort");
        var input = await WriteAsync(folder, "data.json", "{\"key10\":1,\"key2\":{\"z\":1,\"a\":2},\"key1\":3}");

        await Harness.RunCleanAsync("json.sort", input);

        var text = await File.ReadAllTextAsync(Path.Combine(folder, "data_sorted.json"), Token);
        int one = text.IndexOf("\"key1\"", StringComparison.Ordinal);
        int two = text.IndexOf("\"key2\"", StringComparison.Ordinal);
        int ten = text.IndexOf("\"key10\"", StringComparison.Ordinal);
        Assert.True(one < two && two < ten, text);
        Assert.True(text.IndexOf("\"a\"", StringComparison.Ordinal) < text.IndexOf("\"z\"", StringComparison.Ordinal));
    }
}

[Collection(Serial.Name)]
public sealed class JarTests
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
        await ProcessRunner.RunOrThrowAsync(javac!, ["-d", source, Path.Combine(source, "Hello.java")], TestContext.Current.CancellationToken);
        var archive = Path.Combine(folder, "hello.jar");
        await ProcessRunner.RunOrThrowAsync(jar!, ["cf", archive, "-C", source, "Hello.class"], TestContext.Current.CancellationToken);

        await Harness.RunCleanAsync("jar.decompile", archive);

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
