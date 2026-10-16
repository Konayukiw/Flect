using System.Diagnostics;
using System.Text.Json.Nodes;
using Evals.Infrastructure;
using Optimizer.Gui.Dialogs;
using Optimizer.Main.Folder;

namespace Evals;

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

        await Harness.RunCleanAsync("folder.removeDuplicate", folder);

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

        var scan = FileScanner.Scan([folder], new RecordingProgress(), ScanOptions.From(settings.Folder));
        var targets = FileScanner.Outermost(scan.EmptyDirectories);
        Recycler.Delete(targets, DeleteMethod.Permanent);

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

        Recycler.Delete([file], DeleteMethod.RecycleBin);

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
        var task = Harness.Create("folder.tree", folder);

        var progress = await Harness.RunAsync(task, token: Token);

        progress.AssertClean();
        var tree = Harness.Get<string>(task, "_output");
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
        var task = Harness.Create("folder.rename", folder);
        Harness.Set(task, "_settings", new RenameSettings { UseNumbering = true, StartNumber = 1 });
        Harness.Set(task, "_plan", new RenamePlan("photo_", string.Empty, string.Empty, true));

        var progress = await Harness.RunAsync(task, configure: false, token: Token);

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
        var task = Harness.Create("folder.analyze", folder);

        (await Harness.RunAsync(task, token: Token)).AssertClean();

        var report = Harness.Get<AnalyzeReport>(task, "_report");
        Assert.Equal(3, report.FileCount);
        Assert.Equal(1, report.DuplicateGroups);
        Assert.Single(report.EmptyDirectories);
        Assert.EndsWith("big.bin", report.Largest[0].Path);

        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            var output = Path.Combine(folder, "report" + ReportWriter.Extension(format));
            ReportWriter.Write(report, output, format);
            Assert.True(new FileInfo(output).Length > 0);
        }
        var json = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "report.json"), Token))!;
        Assert.Equal(3, json["files"]!.GetValue<int>());
    }
}

[Collection(Serial.Name)]
public sealed class ArchiveTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void RequireSevenZip() =>
        Assert.SkipUnless(Tool.Available(() => Tools.SevenZip), "7za.exe is missing. Run scripts\\fetch.ps1.");

    private static async Task<string> SourceAsync(string parent)
    {
        var source = Path.Combine(parent, "My Folder");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), "alpha", Token);
        await File.WriteAllTextAsync(Path.Combine(source, "sub", "b.txt"), "bravo", Token);
        return source;
    }

    [Theory]
    [InlineData("Zip", "My Folder.zip")]
    [InlineData("SevenZip", "My Folder.7z")]
    [InlineData("Tar", "My Folder.tar")]
    [InlineData("TarGz", "My Folder.tar.gz")]
    public async Task CompressedFoldersExtractBackToTheSameFiles(string format, string archiveName)
    {
        RequireSevenZip();
        TestSettings.Use(settings => settings.Folder.Archive.CompressFormat = Enum.Parse<ArchiveCompressFormat>(format));
        var work = Paths.NewFolder("archive-" + format.ToLowerInvariant());
        var source = await SourceAsync(work);

        await Harness.RunCleanAsync("folder.compress", source);
        var archive = Path.Combine(work, archiveName);
        Assert.True(File.Exists(archive));
        Assert.Empty(Directory.GetDirectories(work, ".Flect-*"));

        await Harness.RunCleanAsync("archive.extract", archive);

        var restored = Directory.GetFiles(work, "b.txt", SearchOption.AllDirectories)
            .Single(path => !path.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("bravo", await File.ReadAllTextAsync(restored, Token));
    }

    [Fact]
    public async Task UnreadableFilesBecomeAWarningAndTheArchiveIsKept()
    {
        RequireSevenZip();
        TestSettings.Use(settings => settings.Folder.Archive.CompressFormat = ArchiveCompressFormat.Zip);
        var work = Paths.NewFolder("archive-locked");
        var source = await SourceAsync(work);

        RecordingProgress progress;
        using (new FileStream(Path.Combine(source, "sub", "b.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            progress = await Harness.RunAsync("folder.compress", source);
        }

        progress.AssertClean();
        Assert.True(File.Exists(Path.Combine(work, "My Folder.zip")));
        var warning = Assert.Single(progress.Warnings);
        Assert.Contains(Path.Combine("My Folder", "sub", "b.txt"), warning);
    }

    [Fact]
    public async Task CancellingCompressionLeavesNothingBehind()
    {
        RequireSevenZip();
        TestSettings.Use(settings => settings.Folder.Archive.CompressFormat = ArchiveCompressFormat.SevenZip);
        var work = Paths.NewFolder("archive-cancel");
        var source = Path.Combine(work, "Big");
        Directory.CreateDirectory(source);
        var noise = new byte[16 * 1024 * 1024];
        for (int index = 0; index < 12; index++)
        {
            Random.Shared.NextBytes(noise);
            await File.WriteAllBytesAsync(Path.Combine(source, $"noise{index}.bin"), noise, Token);
        }
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Harness.RunAsync(Harness.Create("folder.compress", source), token: cancel.Token));

        Assert.False(File.Exists(Path.Combine(work, "Big.7z")));
        Assert.Empty(Directory.GetDirectories(work, ".Flect-*"));
        Paths.DeleteTree(source);
    }

    [Fact]
    public async Task RarExtractsWhenASampleIsProvided()
    {
        var sample = Fixtures.Bundled("sample.rar");
        Assert.SkipWhen(sample is null, "Place evals/fixtures/sample.rar to cover RAR extraction.");
        Assert.SkipUnless(Tool.Available(() => Tools.SevenZipFull), "7z.exe is missing. Run scripts\\fetch.ps1.");
        TestSettings.Use();
        var work = Paths.NewFolder("archive-rar");
        var archive = Path.Combine(work, "sample.rar");
        File.Copy(sample!, archive);

        await Harness.RunCleanAsync("archive.extract", archive);

        Assert.NotEmpty(Directory.GetFiles(Path.Combine(work, "sample"), "*", SearchOption.AllDirectories));
    }
}

[Collection(Serial.Name)]
public sealed class ConfigTests
{
    [Fact]
    public void LocationsFollowFlectHome()
    {
        var home = Path.GetFullPath(Environment.GetEnvironmentVariable("FLECT_HOME")!);
        Assert.Equal(home, Locations.Roaming);
        Assert.Equal(home, Locations.Local);
        Assert.Equal(home, Store.Directory);
    }

    [Fact]
    public void StoreRoundTripsAndSetsCorruptFilesAside()
    {
        var name = $"evals-{Guid.NewGuid():N}.json";
        Store.Write(name, new ArchiveSettings { CompressFormat = ArchiveCompressFormat.TarGz });
        Assert.Equal(ArchiveCompressFormat.TarGz, Store.Read<ArchiveSettings>(name)!.CompressFormat);
        Assert.Empty(Directory.GetFiles(Store.Directory, $".{name}.*.tmp"));

        File.WriteAllText(Path.Combine(Store.Directory, name), "{ broken");
        Assert.Null(Store.Read<ArchiveSettings>(name));
        Assert.True(File.Exists(Path.Combine(Store.Directory, name + ".broken")));
    }

    [Fact]
    public void LegacyDiscordPresetIsRaisedAndMirroredToTheRegistry()
    {
        Assert.SkipUnless(Paths.OnCi, "Writes HKCU\\Software\\Flect, so it runs only on CI.");
        var legacy = new Settings();
        legacy.Video.Presets.CompressDiscord = VideoPresets.LegacyDiscordLimit;
        var json = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(legacy, new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        }))!.AsObject();
        json.Remove("Version");
        File.WriteAllText(Path.Combine(Store.Directory, "settings.json"), json.ToJsonString());

        try
        {
            var loaded = Settings.Load();

            Assert.Equal(VideoPresets.DiscordLimit, loaded.Video.Presets.CompressDiscord);
            Assert.Equal(1, Settings.Load().Version);
            using var presets = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Flect\Presets");
            Assert.Equal(VideoPresets.DiscordLimit.ToString(), presets?.GetValue("Video.CompressDiscord"));
        }
        finally
        {
            File.Delete(Path.Combine(Store.Directory, "settings.json"));
        }
    }
}

[Collection(Serial.Name)]
[Trait("Category", "AI")]
public sealed class AiTests
{
    [Fact]
    public async Task SubtitlesTranscribeSpeech()
    {
        TestSettings.Use(settings =>
        {
            settings.Video.Subtitles.Model = SubtitleModel.Tiny;
            settings.Video.Subtitles.Language = "en";
            settings.Video.Subtitles.Output = SubtitleOutput.Srt;
        });
        var folder = Paths.NewFolder("ai-subtitles");
        var input = await Fixtures.CopyAsync("speech.wav", folder);
        var clock = Stopwatch.StartNew();

        await Harness.RunCleanAsync("audio.subtitles", input);

        var subtitles = await File.ReadAllTextAsync(Path.Combine(folder, "speech.srt"), TestContext.Current.CancellationToken);
        Assert.Contains("-->", subtitles);
        Assert.Matches("(?i)hello|world|subtitle|test", subtitles);
        TestContext.Current.SendDiagnosticMessage($"Transcription finished in {clock.Elapsed.TotalSeconds:0}s");
    }

    [Fact]
    public async Task BackgroundRemovalProducesTransparency()
    {
        TestSettings.Use(settings => settings.Image.BackgroundMode = BackgroundRemovalMode.AiOnly);
        var folder = Paths.NewFolder("ai-background");
        var input = await Fixtures.CopyAsync("photo.jpg", folder);

        await Harness.RunCleanAsync("image.chromakey", input);

        using var output = new ImageMagick.MagickImage(Path.Combine(folder, "photo_keyed.png"));
        Assert.True(output.HasAlpha);
    }
}
