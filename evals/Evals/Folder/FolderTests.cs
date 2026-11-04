using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Evals.Folder;

[Collection(Serial.Name)]
public sealed class FolderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemoveDuplicateKeepsTheOldestCopy()
    {
        TestSettings.Use(settings => settings.Folder.Duplicates.Keep = DuplicateKeep.Oldest);
        var folder = Paths.NewFolder("folder-dups");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        var original = Path.Combine(folder, "a.txt");
        await File.WriteAllTextAsync(original, new string('x', 4096), Token);
        File.Copy(original, Path.Combine(folder, "b.txt"));
        File.Copy(original, Path.Combine(folder, "sub", "d.txt"));
        await File.WriteAllTextAsync(Path.Combine(folder, "c.txt"), new string('y', 4096), Token);
        File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddDays(-10));

        await Rig.RunCleanAsync("folder.removeDuplicate", folder);

        Assert.True(File.Exists(original));
        Assert.True(File.Exists(Path.Combine(folder, "c.txt")));
        Assert.False(File.Exists(Path.Combine(folder, "b.txt")));
        Assert.False(File.Exists(Path.Combine(folder, "sub", "d.txt")));
    }

    [Fact]
    public async Task EmptyFolderScanFindsOnlyTheOutermostEmptyFolders()
    {
        var settings = TestSettings.Use();
        var folder = Paths.NewFolder("folder-empty");
        Directory.CreateDirectory(Path.Combine(folder, "empty", "nested"));
        Directory.CreateDirectory(Path.Combine(folder, "kept"));
        await File.WriteAllTextAsync(Path.Combine(folder, "kept", "file.txt"), "x", Token);
        Directory.CreateDirectory(Path.Combine(folder, "ini-only"));
        await File.WriteAllTextAsync(Path.Combine(folder, "ini-only", "desktop.ini"), "[.ShellClassInfo]", Token);

        var scan = Flect.Folder.Scan.ScanTree([folder], new RecordingProgress(), ScanOptions.From(settings.Folder));
        var targets = Flect.Folder.Scan.Outermost(scan.EmptyDirectories);
        Flect.Files.Recycle.Delete(targets, DeleteMethod.Permanent);

        Assert.Equal(2, targets.Count);
        Assert.False(Directory.Exists(Path.Combine(folder, "empty")));
        Assert.False(Directory.Exists(Path.Combine(folder, "ini-only")));
        Assert.True(Directory.Exists(Path.Combine(folder, "kept")));
    }

    [Fact]
    public async Task RecyclerMovesFilesToTheRecycleBin()
    {
        Assert.SkipUnless(Paths.OnCi, "Runs only on CI so local Recycle Bins stay untouched.");
        var folder = Paths.NewFolder("folder-recycle");
        var file = Path.Combine(folder, "recycle-me.txt");
        await File.WriteAllTextAsync(file, "x", Token);

        Flect.Files.Recycle.Delete([file], DeleteMethod.RecycleBin);

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task TreePrintsTheFolderStructure()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("folder-tree");
        Directory.CreateDirectory(Path.Combine(folder, "docs"));
        await File.WriteAllTextAsync(Path.Combine(folder, "docs", "readme.md"), "x", Token);
        await File.WriteAllTextAsync(Path.Combine(folder, "root.txt"), "x", Token);
        var task = Rig.Create("folder.tree", folder);

        var progress = await Rig.RunAsync(task, token: Token);

        progress.AssertClean();
        var tree = Rig.Get<string>(task, "_output");
        Assert.Contains("├───docs", tree);
        Assert.Contains("readme.md", tree);
        Assert.Contains("└───root.txt", tree);
    }

    [Fact]
    public async Task RenameNumbersFilesInExplorerOrder()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("folder-rename");
        foreach (var number in new[] { "1", "2", "10" })
        {
            await File.WriteAllTextAsync(Path.Combine(folder, $"img{number}.png"), number, Token);
        }
        var task = Rig.Create("folder.rename", folder);
        Rig.Set(task, "_settings", new RenameSettings { UseNumbering = true, StartNumber = 1 });
        Rig.Set(task, "_plan", new RenamePlan("photo_", string.Empty, string.Empty, true));

        var progress = await Rig.RunAsync(task, configure: false, token: Token);

        progress.AssertClean();
        Assert.Equal("1", await File.ReadAllTextAsync(Path.Combine(folder, "photo_1.png"), Token));
        Assert.Equal("2", await File.ReadAllTextAsync(Path.Combine(folder, "photo_2.png"), Token));
        Assert.Equal("10", await File.ReadAllTextAsync(Path.Combine(folder, "photo_3.png"), Token));
    }

    [Fact]
    public async Task AnalyzeBuildsAReportInEveryFormat()
    {
        TestSettings.Use();
        var folder = Paths.NewFolder("folder-analyze");
        await File.WriteAllTextAsync(Path.Combine(folder, "big.bin"), new string('b', 50_000), Token);
        await File.WriteAllTextAsync(Path.Combine(folder, "copy1.txt"), new string('c', 2048), Token);
        await File.WriteAllTextAsync(Path.Combine(folder, "copy2.txt"), new string('c', 2048), Token);
        Directory.CreateDirectory(Path.Combine(folder, "nothing"));
        var task = Rig.Create("folder.analyze", folder);

        (await Rig.RunAsync(task, token: Token)).AssertClean();

        var report = Rig.Get<Flect.Folder.Analysis.Analysis>(task, "_report");
        Assert.Equal(3, report.FileCount);
        Assert.Equal(1, report.DuplicateGroups);
        Assert.Single(report.EmptyDirectories);
        Assert.EndsWith("big.bin", report.Largest[0].Path);

        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            var output = Path.Combine(folder, "report" + Flect.Folder.Analysis.AnalysisFile.Extension(format));
            Flect.Folder.Analysis.AnalysisFile.Write(report, output, format);
            Assert.True(new FileInfo(output).Length > 0);
        }
        var json = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "report.json"), Token))!;
        Assert.Equal(3, json["files"]!.GetValue<int>());
    }
}
