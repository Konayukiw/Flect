namespace Flect.Settings.General;

internal sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public AppLanguage Language { get; set; } = AppLanguage.System;

    public bool CloseOnSuccess { get; set; } = true;
    public bool CloseOnFailure { get; set; }
    public bool CloseOnCancel { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;

    public List<string> HiddenMenuItems { get; set; } = [];
}
