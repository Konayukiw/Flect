using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Evals.Archive;

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

        await Rig.RunCleanAsync("folder.compress", source);
        var archive = Path.Combine(work, archiveName);
        Assert.True(File.Exists(archive));
        Assert.Empty(Directory.GetDirectories(work, ".Flect-*"));

        await Rig.RunCleanAsync("archive.extract", archive);

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
            progress = await Rig.RunAsync("folder.compress", source);
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
            Rig.RunAsync(Rig.Create("folder.compress", source), token: cancel.Token));

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

        await Rig.RunCleanAsync("archive.extract", archive);

        Assert.NotEmpty(Directory.GetFiles(Path.Combine(work, "sample"), "*", SearchOption.AllDirectories));
    }
}
