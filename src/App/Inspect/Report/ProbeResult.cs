namespace Flect.Inspect.Report;

internal sealed class ProbeResult
{
    public string? Kind { get; init; }
    public bool Executable { get; init; }
    public IReadOnlyList<FileSection> Sections { get; init; } = [];
}
