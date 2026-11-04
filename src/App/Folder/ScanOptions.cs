using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Hashing;

namespace Flect.Folder;

internal sealed record ScanOptions(Exclusion Exclusions, EmptyFolderSettings Empty)
{
    public static ScanOptions From(FolderSettings folder) =>
        new(Exclusion.From(folder.ScanExclusions), folder.Empty);

    public static ScanOptions From(FolderSettings folder, ExclusionRules extra) =>
        new(Exclusion.From(Merge(folder.ScanExclusions, extra)), folder.Empty);

    private static ExclusionRules Merge(ExclusionRules first, ExclusionRules second) => new()
    {
        Folders = Join(first.Folders, second.Folders),
        Files = Join(first.Files, second.Files),
        Extensions = Join(first.Extensions, second.Extensions),
    };

    private static string Join(string first, string second) =>
        string.Join('\n', Exclusion.Split(first).Concat(Exclusion.Split(second)));
}
