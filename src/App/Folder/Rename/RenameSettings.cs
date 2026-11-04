namespace Flect.Folder.Rename;

internal sealed class RenameSettings
{
    public bool UseNumbering { get; set; } = true;
    public int DigitCount { get; set; }
    public int StartNumber { get; set; } = 1;
    public bool DryRun { get; set; }
    public ExclusionRules Exclusions { get; set; } = new();
}
