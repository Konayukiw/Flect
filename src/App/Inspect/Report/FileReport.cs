namespace Flect.Inspect.Report;

internal sealed class FileReport
{
    public required string Path { get; init; }
    public required IReadOnlyList<string> Alerts { get; init; }
    public required IReadOnlyList<FileFact> Overview { get; init; }
    public required IReadOnlyList<FileSection> Sections { get; init; }
}
