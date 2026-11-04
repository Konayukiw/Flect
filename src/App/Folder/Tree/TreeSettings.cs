namespace Flect.Folder.Tree;

internal sealed class TreeSettings
{
    public bool FoldersOnly { get; set; }
    public ExclusionRules Exclusions { get; set; } = new();
}
