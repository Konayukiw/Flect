namespace Flect.Inspect.Report;

internal sealed class FactBag
{
    private readonly List<FileFact> _rows = [];

    public void Add(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _rows.Add(new FileFact(Loc.T(key), value.Trim(), key));
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
