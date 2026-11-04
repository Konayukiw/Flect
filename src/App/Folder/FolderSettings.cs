namespace Flect.Folder;

internal sealed class FolderSettings
{
    public DuplicateSettings Duplicates { get; set; } = new();
    public DeleteMethod DeleteMethod { get; set; } = DeleteMethod.RecycleBin;
    public ExclusionRules ScanExclusions { get; set; } = new();
    public RenameSettings Rename { get; set; } = new();
    public EmptyFolderSettings Empty { get; set; } = new();
    public TreeSettings Tree { get; set; } = new();
    public AnalyzeSettings Analyze { get; set; } = new();
    public ArchiveSettings Archive { get; set; } = new();
}
