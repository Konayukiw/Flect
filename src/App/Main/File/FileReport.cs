namespace Optimizer.Main.Inspect;

internal sealed record FileFact(string Label, string Value);

internal sealed class FileSection
{
    public required string Title { get; init; }
    public required IReadOnlyList<FileFact> Rows { get; init; }
}

internal sealed class FileReport
{
    public required string Path { get; init; }
    public required IReadOnlyList<string> Alerts { get; init; }
    public required IReadOnlyList<FileFact> Overview { get; init; }
    public required IReadOnlyList<FileSection> Sections { get; init; }
}

internal sealed class FactBag
{
    private readonly List<FileFact> _rows = [];

    public void Add(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _rows.Add(new FileFact(Loc.T(key), value.Trim()));
    }

    public void AddRaw(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value)) return;
        _rows.Add(new FileFact(label.Trim(), value.Trim()));
    }

    public FileSection? Section(string titleKey)
    {
        if (_rows.Count == 0) return null;
        return new FileSection { Title = Loc.T(titleKey), Rows = _rows.ToList() };
    }

    public IReadOnlyList<FileFact> Rows => _rows;
}

internal sealed class ProbeResult
{
    public string? Kind { get; init; }
    public bool Executable { get; init; }
    public IReadOnlyList<FileSection> Sections { get; init; } = [];
}
