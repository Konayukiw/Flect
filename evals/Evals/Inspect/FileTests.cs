using System.IO.Compression;
using System.Text;
using ImageMagick;
using Flect.I18n;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace Evals.Inspect;

[Collection(Serial.Name)]
public sealed class FileTests
{
    public FileTests() => Loc.Use(AppLanguage.English);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PngRenamedToWebpIsReportedAsPng()
    {
        var folder = Paths.NewFolder("file-png");
        var png = Path.Combine(folder, "real.png");
        using (var image = new MagickImage(MagickColors.Red, 12, 7))
        {
            image.Write(png, MagickFormat.Png);
        }
        var spoof = Path.Combine(folder, "spoof.webp");
        File.Copy(png, spoof);
        var before = new FileInfo(spoof);
        var stamp = before.LastWriteTimeUtc;

        var report = await Flect.Inspect.Examine.InspectAsync(spoof, Token);

        Assert.Equal(before.Length, new FileInfo(spoof).Length);
        Assert.Equal(stamp, new FileInfo(spoof).LastWriteTimeUtc);
        Assert.Contains(report.Alerts, alert => alert.Contains("WEBP") && alert.Contains("PNG"));
        Assert.Equal("PNG", Value(report, "Contents"));
        Assert.Equal("12 x 7", Value(report, "Dimensions"));
    }

    [Fact]
    public async Task RightToLeftOverrideShowsTheSpoofedName()
    {
        var folder = Paths.NewFolder("file-rlo");
        var path = Path.Combine(folder, "evil\u202Egpj.exe");
        await File.WriteAllBytesAsync(path, PortableExecutable(), Token);

        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);

        Assert.Contains(report.Alerts, alert => alert.Contains("RLO"));
        Assert.Contains(report.Alerts, alert => alert.Contains("JPEG"));
        Assert.Equal("evilexe.jpg", Value(report, "Shown name"));
        Assert.Equal("PE", Value(report, "Contents"));
        Assert.Equal("AMD64", Value(report, "Machine"));
        Assert.Equal("Console", Value(report, "Subsystem"));
        Assert.DoesNotContain(report.Alerts, alert => alert.Contains("extension says EXE"));
    }

    [Fact]
    public async Task DoubleExtensionWarnsWhenAnExecutableHidesBehindAnotherType()
    {
        var folder = Paths.NewFolder("file-double");
        var path = Path.Combine(folder, "photo.jpg.exe");
        await File.WriteAllBytesAsync(path, PortableExecutable(), Token);

        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);

        Assert.Contains(report.Alerts, alert => alert.Contains("JPEG"));
        Assert.Equal("EXE", Value(report, "Extension"));
        Assert.Equal("PE", Value(report, "Contents"));
    }

    [Fact]
    public async Task ExecutableBytesWithAnImageExtensionAreCalledOut()
    {
        var folder = Paths.NewFolder("file-pe");
        var path = Path.Combine(folder, "note.png");
        await File.WriteAllBytesAsync(path, PortableExecutable(), Token);

        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);

        Assert.Contains(report.Alerts, alert => alert.Contains("PNG") && alert.Contains("PE"));
        Assert.Contains(report.Alerts, alert => alert.Contains("presented as PNG"));
        Assert.Equal("Native", Value(report, "Runtime"));
        Assert.Equal("No", Value(report, "Signature"));
    }

    [Fact]
    public async Task MixedScriptLookalikeIsReportedAndACyrillicNameIsNot()
    {
        var folder = Paths.NewFolder("file-glyph");
        var mixed = Path.Combine(folder, "report\u0430.txt");
        var cyrillic = Path.Combine(folder, "\u043F\u0440\u0438\u0432\u0435\u0442.txt");
        await File.WriteAllTextAsync(mixed, "a", Token);
        await File.WriteAllTextAsync(cyrillic, "a", Token);

        var mixedReport = await Flect.Inspect.Examine.InspectAsync(mixed, Token);
        var cyrillicReport = await Flect.Inspect.Examine.InspectAsync(cyrillic, Token);

        Assert.Contains(mixedReport.Alerts, alert => alert.Contains("U+0430"));
        Assert.DoesNotContain(cyrillicReport.Alerts, alert => alert.Contains("lookalike"));
    }

    [Fact]
    public async Task TextJsonAndZoneAreReadWithoutRewritingTheFile()
    {
        var folder = Paths.NewFolder("file-text");
        var json = Path.Combine(folder, "data.json");
        await File.WriteAllTextAsync(json, "{\"b\":1,\"a\":[2,3]}\n", new UTF8Encoding(false), Token);
        var lines = Path.Combine(folder, "notes.txt");
        await File.WriteAllTextAsync(lines, "a\r\nb\r\n", new UTF8Encoding(true), Token);
        var csv = Path.Combine(folder, "table.csv");
        await File.WriteAllTextAsync(csv, "a,b,c\n1,2,3\n", Token);
        var ini = Path.Combine(folder, "app.ini");
        await File.WriteAllTextAsync(ini, "[main]\nname=flect\ncount=2\n", Token);
        await File.WriteAllTextAsync(lines + ":Zone.Identifier",
                                      "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://example.test/a\r\n", Token);

        var jsonReport = await Flect.Inspect.Examine.InspectAsync(json, Token);
        Assert.Equal("Yes", Value(jsonReport, "Valid"));
        Assert.Equal("Object", Value(jsonReport, "Top level"));
        Assert.Equal("2", Value(jsonReport, "Count"));
        Assert.Equal("2", Value(jsonReport, "Depth"));
        Assert.Empty(jsonReport.Alerts);

        var textReport = await Flect.Inspect.Examine.InspectAsync(lines, Token);
        Assert.Equal("CRLF", Value(textReport, "Line endings"));
        Assert.Equal("2", Value(textReport, "Lines"));
        Assert.Equal("UTF-8", Value(textReport, "BOM"));
        Assert.Equal("Internet", Value(textReport, "Mark of the Web"));
        Assert.Equal("https://example.test/a", Value(textReport, "Downloaded from"));

        var csvReport = await Flect.Inspect.Examine.InspectAsync(csv, Token);
        Assert.Equal("2", Value(csvReport, "Rows"));
        Assert.Equal("3", Value(csvReport, "Columns"));

        var iniReport = await Flect.Inspect.Examine.InspectAsync(ini, Token);
        Assert.Equal("1", Value(iniReport, "Sections"));
        Assert.Equal("2", Value(iniReport, "Keys"));
    }

    [Fact]
    public async Task JpegCameraTagIsListed()
    {
        var folder = Paths.NewFolder("file-exif");
        var path = Path.Combine(folder, "camera.jpg");
        using (var image = new MagickImage(MagickColors.Blue, 8, 6))
        {
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.Make, "FlectCam");
            image.SetProfile(profile);
            image.Write(path, MagickFormat.Jpeg);
        }

        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);

        Assert.Equal("JPEG", Value(report, "Contents"));
        Assert.Equal("8 x 6", Value(report, "Dimensions"));
        Assert.Equal("FlectCam", Value(report, "Make"));
        Assert.Empty(report.Alerts);
    }

    [Fact]
    public async Task SvgReportsStructure()
    {
        var folder = Paths.NewFolder("file-svg");
        var path = Path.Combine(folder, "icon.svg");
        await File.WriteAllTextAsync(path,
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="20" viewBox="0 0 10 20">
              <image href="https://example.test/a.png"/>
              <script></script>
            </svg>
            """, Token);

        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);

        Assert.Equal("SVG", Value(report, "Contents"));
        Assert.Equal("0 0 10 20", Value(report, "viewBox"));
        Assert.Equal("1", Value(report, "Scripts"));
        Assert.Equal("1", Value(report, "External references"));
        Assert.Empty(report.Alerts);
    }

    [Fact]
    public async Task ZipListsTheLargestEntryAndJarReadsTheManifest()
    {
        var folder = Paths.NewFolder("file-zip");
        var zipPath = Path.Combine(folder, "pack.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "big.txt", new string('x', 80));
            WriteEntry(zip, "small.txt", "y");
        }
        var jarPath = Path.Combine(folder, "app.jar");
        using (var jar = ZipFile.Open(jarPath, ZipArchiveMode.Create))
        {
            WriteEntry(jar, "META-INF/MANIFEST.MF",
                       "Manifest-Version: 1.0\nMain-Class: demo.App\nImplementation-Version: 1.2.3\n");
            var code = jar.CreateEntry("demo/App.class");
            using var stream = code.Open();
            stream.Write(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x41 });
        }

        var zipReport = await Flect.Inspect.Examine.InspectAsync(zipPath, Token);
        Assert.Equal("ZIP", Value(zipReport, "Contents"));
        Assert.Equal("2", Value(zipReport, "Files"));
        Assert.Contains("big.txt", Value(zipReport, "Largest items"));
        Assert.Equal("No", Value(zipReport, "Encrypted"));

        var jarReport = await Flect.Inspect.Examine.InspectAsync(jarPath, Token);
        Assert.Equal("JAR", Value(jarReport, "Contents"));
        Assert.Equal("demo.App", Value(jarReport, "Main class"));
        Assert.Equal("1.2.3", Value(jarReport, "Version"));
        Assert.Equal("65 (Java 21)", Value(jarReport, "Bytecode"));
        Assert.Equal("No", Value(jarReport, "Signed"));
    }

    [Fact]
    public async Task PdfReportsPageSizeAndBookmarkCount()
    {
        var folder = Paths.NewFolder("file-pdf");
        var path = Path.Combine(folder, "doc.pdf");
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        await File.WriteAllBytesAsync(path, builder.Build(), Token);

        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);

        Assert.Equal("PDF", Value(report, "Contents"));
        Assert.Equal("1", Value(report, "Pages"));
        Assert.Contains("A4", Value(report, "Page size"));
        Assert.Equal("0", Value(report, "Bookmarks"));
        Assert.Equal("No", Value(report, "Encrypted"));
        Assert.Equal("Yes", Value(report, "Printing"));
    }

    [Fact]
    public async Task Sha256IsComputedForTheFileBytes()
    {
        var folder = Paths.NewFolder("file-hash");
        var path = Path.Combine(folder, "a.bin");
        await File.WriteAllTextAsync(path, "a", new UTF8Encoding(false), Token);

        var hash = Flect.Inspect.Checksum.Sha256(path, Token);

        Assert.Equal("ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb", hash);
    }

    [Fact]
    public async Task SavedReportIncludesTheHashInEveryFormat()
    {
        var folder = Paths.NewFolder("file-report");
        var path = Path.Combine(folder, "note.txt");
        await File.WriteAllTextAsync(path, "hello", Token);
        var report = await Flect.Inspect.Examine.InspectAsync(path, Token);
        var hash = Flect.Inspect.Checksum.Sha256(path, Token);

        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            var output = Path.Combine(folder, "report" + Flect.Folder.Analysis.AnalysisFile.Extension(format));
            Flect.Inspect.Report.ReportFile.Write(report, hash, output, format);
            var text = await File.ReadAllTextAsync(output, Token);
            Assert.Contains(hash, text);
        }
    }

    [Fact]
    public async Task TaskBuildsTheSameReport()
    {
        var folder = Paths.NewFolder("file-task");
        var path = Path.Combine(folder, "note.txt");
        await File.WriteAllTextAsync(path, "hello", Token);
        var task = Rig.Create("file.analyze", path);

        (await Rig.RunAsync(task, token: Token)).AssertClean();

        var report = Rig.Get<FileReport>(task, "_report");
        Assert.Equal("TXT", report.Overview.Single(fact => fact.Label == "Extension").Value);
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string Value(FileReport report, string label)
    {
        foreach (var fact in report.Overview)
        {
            if (fact.Label == label) return fact.Value;
        }
        foreach (var section in report.Sections)
        {
            foreach (var fact in section.Rows)
            {
                if (fact.Label == label) return fact.Value;
            }
        }
        throw new InvalidOperationException("Missing row: " + label + " in " +
                                            string.Join(" | ", report.Sections.Select(section => section.Title)));
    }

    private static byte[] PortableExecutable()
    {
        var bytes = new byte[0x58 + 0xF0];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3C] = 0x40;
        bytes[0x40] = (byte)'P';
        bytes[0x41] = (byte)'E';
        bytes[0x44] = 0x64;
        bytes[0x45] = 0x86;
        bytes[0x54] = 0xF0;
        bytes[0x56] = 0x22;
        bytes[0x58] = 0x0B;
        bytes[0x59] = 0x02;
        bytes[0x9C] = 3;
        bytes[0xC4] = 16;
        return bytes;
    }
}
