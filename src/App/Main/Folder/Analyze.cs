namespace Optimizer.Main.Folder;

internal sealed record TypeShare(string Extension, int Count, long Bytes);

internal sealed class AnalyzeReport
{
    public required IReadOnlyList<string> Roots { get; init; }
    public required long TotalBytes { get; init; }
    public required int FileCount { get; init; }
    public required int DirectoryCount { get; init; }
    public required IReadOnlyList<ScannedFile> Largest { get; init; }
    public required IReadOnlyList<TypeShare> ByType { get; init; }
    public required int DuplicateGroups { get; init; }
    public required int DuplicateFiles { get; init; }
    public required long ReclaimableBytes { get; init; }
    public required IReadOnlyList<string> EmptyDirectories { get; init; }
    public required TimeSpan Elapsed { get; init; }

    public const int TopCount = 15;

    public static AnalyzeReport Build(IReadOnlyList<string> roots, TreeScan scan,
                                      IReadOnlyList<List<ScannedFile>> duplicates,
                                      TimeSpan elapsed)
    {
        var byType = scan.Files
            .GroupBy(file => file.Extension.Length == 0 ? "(no extension)" : file.Extension)
            .Select(group => new TypeShare(group.Key, group.Count(), group.Sum(file => file.Size)))
            .OrderByDescending(share => share.Bytes)
            .ToList();

        long reclaimable = duplicates.Sum(group => group.Sum(file => file.Size) - group[0].Size);

        return new AnalyzeReport
        {
            Roots = roots,
            TotalBytes = scan.TotalBytes,
            FileCount = scan.Files.Count,
            DirectoryCount = scan.DirectoryCount,
            Largest = scan.Files.OrderByDescending(file => file.Size).Take(TopCount).ToList(),
            ByType = byType,
            DuplicateGroups = duplicates.Count,
            DuplicateFiles = duplicates.Sum(group => group.Count),
            ReclaimableBytes = reclaimable,
            EmptyDirectories = scan.EmptyDirectories,
            Elapsed = elapsed,
        };
    }
}
