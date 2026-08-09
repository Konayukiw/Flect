using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Hashing;

namespace Optimizer.Main;

internal sealed record ScannedFile(string Path, long Size, DateTime LastWrite, string Extension);

internal sealed class TreeScan
{
    public List<ScannedFile> Files { get; } = [];
    public List<string> EmptyDirectories { get; } = [];
    public int DirectoryCount { get; set; }

    public long TotalBytes => Files.Sum(file => file.Size);
}

internal static class FileScanner
{
    private const int HeadHashBytes = 64 * 1024;

    public static TreeScan Scan(IEnumerable<string> roots, ITaskProgress progress)
    {
        var scan = new TreeScan();
        foreach (var root in roots)
        {
            progress.Token.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) continue;
            Walk(new DirectoryInfo(root), scan, progress, isRoot: true);
        }
        return scan;
    }

    private static bool Walk(DirectoryInfo directory, TreeScan scan, ITaskProgress progress,
                             bool isRoot)
    {
        progress.Token.ThrowIfCancellationRequested();

        bool hasFiles = false;
        try
        {
            foreach (var file in directory.EnumerateFiles())
            {
                if ((file.Attributes & FileAttributes.System) != 0) continue;
                hasFiles = true;
                scan.Files.Add(new ScannedFile(file.FullName, file.Length, file.LastWriteTime,
                                               Extension(file.Name)));
                if (scan.Files.Count % 4096 == 0)
                {
                    progress.Status($"Scanning — {Formatting.Plural(scan.Files.Count, "file")}");
                }
            }

            foreach (var child in SafeSubdirectories(directory))
            {
                scan.DirectoryCount++;
                if (Walk(child, scan, progress, isRoot: false)) hasFiles = true;
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

    private static IEnumerable<DirectoryInfo> SafeSubdirectories(DirectoryInfo directory)
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
                                                         ITaskProgress progress)
    {
        var candidates = files.GroupBy(file => file.Size)
                              .Where(group => group.Count() > 1)
                              .SelectMany(group => group)
                              .ToList();
        if (candidates.Count == 0) return [];

        progress.Status($"Comparing {Formatting.Count(candidates.Count)} same-size files");
        var byHead = HashGroups(candidates, HeadHashBytes, progress);
        var confirmed = byHead.Where(group => group[0].Size <= HeadHashBytes).ToList();
        var unverified = byHead.Where(group => group[0].Size > HeadHashBytes)
                               .SelectMany(group => group)
                               .ToList();
        if (unverified.Count == 0) return confirmed;

        progress.Status($"Verifying {Formatting.Plural(unverified.Count, "candidate")}");
        confirmed.AddRange(HashGroups(unverified, long.MaxValue, progress));
        return confirmed;
    }

    private static List<List<ScannedFile>> HashGroups(IReadOnlyList<ScannedFile> files, long limit,
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
                    .GroupBy(file => (file.Size, hashes[file.Path]))
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
