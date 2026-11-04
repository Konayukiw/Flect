namespace Flect.Folder.Duplicate;

internal sealed class DuplicateSettings
{
    public DuplicateStrictness Strictness { get; set; } = DuplicateStrictness.FullContent;
    public bool MatchName { get; set; }
    public bool MatchTimestamp { get; set; }
    public bool IgnoreEmptyFiles { get; set; } = true;
    public long MinimumBytes { get; set; }
    public DuplicateKeep Keep { get; set; } = DuplicateKeep.Ask;
}
