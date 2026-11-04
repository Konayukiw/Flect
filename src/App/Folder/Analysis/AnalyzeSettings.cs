namespace Flect.Folder.Analysis;

internal sealed class AnalyzeSettings
{
    public ReportFormat SaveFormat { get; set; } = ReportFormat.Csv;
    public ExclusionRules Exclusions { get; set; } = new();
}
