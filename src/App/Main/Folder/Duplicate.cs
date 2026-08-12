namespace Optimizer.Main.Folder;

internal enum DuplicateMode { KeepOldest, KeepNewest, DeleteAll }

internal static class DuplicatePlan
{
    public static List<ScannedFile> ToDelete(IEnumerable<List<ScannedFile>> groups,
                                             DuplicateMode mode)
    {
        var doomed = new List<ScannedFile>();
        foreach (var group in groups)
        {
            switch (mode)
            {
                case DuplicateMode.DeleteAll:
                    doomed.AddRange(group);
                    break;

                case DuplicateMode.KeepOldest:
                {
                    var keep = group.MinBy(file => file.LastWrite)!;
                    doomed.AddRange(group.Where(file => !ReferenceEquals(file, keep)));
                    break;
                }

                case DuplicateMode.KeepNewest:
                {
                    var keep = group.MaxBy(file => file.LastWrite)!;
                    doomed.AddRange(group.Where(file => !ReferenceEquals(file, keep)));
                    break;
                }
            }
        }
        return doomed;
    }
}
