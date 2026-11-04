namespace Flect.Inspect.Report;

internal sealed class FileSection
{
    public required string Title { get; init; }
    public required IReadOnlyList<FileFact> Rows { get; init; }
}
