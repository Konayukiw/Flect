using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Hashing;

namespace Optimizer.Main.Folder;

internal sealed record ScannedFile(string Path, long Size, DateTime LastWrite, string Extension);

internal sealed class TreeScan
{
    public List<ScannedFile> Files { get; } = [];
    public List<string> EmptyDirectories { get; } = [];
    public int DirectoryCount { get; set; }

    public long TotalBytes => Files.Sum(file => file.Size);
}

internal sealed record ScanOptions(ExclusionFilter Exclusions, EmptyFolderSettings Empty)
{
    public static ScanOptions From(FolderSettings folder) =>
        new(ExclusionFilter.From(folder.ScanExclusions), folder.Empty);

    public static ScanOptions From(FolderSettings folder, ExclusionRules extra) =>
        new(ExclusionFilter.From(Merge(folder.ScanExclusions, extra)), folder.Empty);

    private static ExclusionRules Merge(ExclusionRules first, ExclusionRules second) => new()
    {
        Folders = Join(first.Folders, second.Folders),
        Files = Join(first.Files, second.Files),
        Extensions = Join(first.Extensions, second.Extensions),
    };

    private static string Join(string first, string second) =>
        string.Join('\n', ExclusionFilter.Split(first).Concat(ExclusionFilter.Split(second)));
}

internal static class FileScanner
{
    private const int HeadHashBytes = 64 * 1024;

    public static TreeScan Scan(IEnumerable<string> roots, ITaskProgress progress,
                                ScanOptions options)
    {
        var scan = new TreeScan();
        foreach (var root in roots)
        {
            progress.Token.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) continue;
            Walk(new DirectoryInfo(root), scan, progress, options, isRoot: true);
        }
        return scan;
    }

    private static bool Walk(DirectoryInfo directory, TreeScan scan, ITaskProgress progress,
                             ScanOptions options, bool isRoot)
    {
        progress.Token.ThrowIfCancellationRequested();

        bool hasFiles = false;
        try
        {
            foreach (var file in directory.EnumerateFiles())
            {
                if ((file.Attributes & FileAttributes.System) != 0) continue;

                if (!Ignorable(file, options.Empty)) hasFiles = true;
                if (options.Exclusions.ExcludesFile(file.Name)) continue;

                scan.Files.Add(new ScannedFile(file.FullName, file.Length, file.LastWriteTime,
                                               Extension(file.Name)));
                if (scan.Files.Count % 4096 == 0)
                {
                    progress.Status(Loc.F("msg.scanningCount", Loc.N("count.file", scan.Files.Count)));
                }
            }

            foreach (var child in SafeSubdirectories(directory, options.Exclusions))
            {
                scan.DirectoryCount++;
                if (Walk(child, scan, progress, options, isRoot: false)) hasFiles = true;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }

        if (!hasFiles && !isRoot) scan.EmptyDirectories.Add(directory.FullName);
        return hasFiles;
    }

    private static bool Ignorable(FileInfo file, EmptyFolderSettings empty)
    {
        if (empty.IgnoreZeroByteFiles && file.Length == 0) return true;
        if (empty.IgnoreDesktopIni &&
            string.Equals(file.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return empty.IgnoreThumbsDb &&
               string.Equals(file.Name, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<DirectoryInfo> SafeSubdirectories(DirectoryInfo directory,
                                                                 ExclusionFilter exclusions)
    {
        List<DirectoryInfo> children;
        try
        {
            children = directory.EnumerateDirectories().ToList();
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var child in children)
        {
            if ((child.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            if (exclusions.ExcludesFolder(child.Name)) continue;
            yield return child;
        }
    }

    public static string Extension(string name)
    {
        var extension = Path.GetExtension(name);
        return extension.Length > 1 ? extension[1..].ToLowerInvariant() : string.Empty;
    }

    public static List<string> Outermost(IEnumerable<string> directories)
    {
        var all = new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase);
        return all.Where(directory =>
                 {
                     var parent = Path.GetDirectoryName(directory);
                     return parent is null || !all.Contains(parent);
                 })
                 .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
                 .ToList();
    }

    public static List<List<ScannedFile>> FindDuplicates(IReadOnlyList<ScannedFile> files,
                                                         ITaskProgress progress,
                                                         DuplicateSettings settings)
    {
        var eligible = files
            .Where(file => !(settings.IgnoreEmptyFiles && file.Size == 0))
            .Where(file => file.Size >= settings.MinimumBytes)
            .ToList();

        var candidates = eligible.GroupBy(file => Identity(file, settings))
                                 .Where(group => group.Count() > 1)
                                 .SelectMany(group => group)
                                 .ToList();
        if (candidates.Count == 0) return [];

        if (settings.Strictness == DuplicateStrictness.SizeOnly)
        {
            return candidates.GroupBy(file => Identity(file, settings))
                             .Select(group => group.ToList())
                             .ToList();
        }

        progress.Status(Loc.F("msg.comparing", Loc.N("count.file", candidates.Count)));
        var byHead = HashGroups(candidates, HeadHashBytes, settings, progress);

        if (settings.Strictness == DuplicateStrictness.HeadHash) return byHead;

        var confirmed = byHead.Where(group => group[0].Size <= HeadHashBytes).ToList();
        var unverified = byHead.Where(group => group[0].Size > HeadHashBytes)
                               .SelectMany(group => group)
                               .ToList();
        if (unverified.Count == 0) return confirmed;

        progress.Status(Loc.F("msg.verifying", Loc.N("count.candidate", unverified.Count)));
        confirmed.AddRange(HashGroups(unverified, long.MaxValue, settings, progress));
        return confirmed;
    }

    private static string Identity(ScannedFile file, DuplicateSettings settings)
    {
        var key = file.Size.ToString(CultureInfo.InvariantCulture);

        if (settings.MatchName)
        {
            key += "|" + Path.GetFileName(file.Path).ToLowerInvariant();
        }
        if (settings.MatchTimestamp)
        {
            key += "|" + file.LastWrite.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        }
        return key;
    }

    private static List<List<ScannedFile>> HashGroups(IReadOnlyList<ScannedFile> files, long limit,
                                                      DuplicateSettings settings,
                                                      ITaskProgress progress)
    {
        var hashes = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int done = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
            CancellationToken = progress.Token,
        };

        Parallel.ForEach(files, options, file =>
        {
            try
            {
                hashes[file.Path] = Hash(file.Path, limit, progress.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
            }

            int completed = Interlocked.Increment(ref done);
            if (completed % 128 == 0) progress.Step(completed, files.Count);
        });

        return files.Where(file => hashes.ContainsKey(file.Path))
                    .GroupBy(file => (Identity(file, settings), hashes[file.Path]))
                    .Where(group => group.Count() > 1)
                    .Select(group => group.ToList())
                    .ToList();
    }

    private static string Hash(string path, long limit, CancellationToken token)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete,
                                          bufferSize: 0, FileOptions.SequentialScan);
        var hasher = new XxHash128();
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            long remaining = limit;
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0) break;
                hasher.Append(buffer.AsSpan(0, read));
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return Convert.ToHexString(hasher.GetCurrentHash());
    }
}
