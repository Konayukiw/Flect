namespace Flect.Folder.Empty;

internal sealed class EmptyFolderSettings
{
    public bool IgnoreZeroByteFiles { get; set; }
    public bool IgnoreDesktopIni { get; set; } = true;
    public bool IgnoreThumbsDb { get; set; } = true;
}
