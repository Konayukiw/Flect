namespace Optimizer.Main.Folder;

internal sealed class ExclusionFilter
{
    public static readonly ExclusionFilter None = new([], [], []);

    private readonly string[] _folders;
    private readonly string[] _files;
    private readonly string[] _extensions;

    private ExclusionFilter(string[] folders, string[] files, string[] extensions)
    {
        _folders = folders;
        _files = files;
        _extensions = extensions;
    }

    public bool IsEmpty => _folders.Length == 0 && _files.Length == 0 && _extensions.Length == 0;

    public static ExclusionFilter From(ExclusionRules? rules)
    {
        if (rules is null) return None;

        var folders = Split(rules.Folders);
        var files = Split(rules.Files);
        var extensions = Split(rules.Extensions)
            .Select(entry => entry.StartsWith('.') ? entry : "." + entry)
            .ToArray();

        return folders.Length == 0 && files.Length == 0 && extensions.Length == 0
            ? None
            : new ExclusionFilter(folders, files, extensions);
    }

    public bool ExcludesFolder(string name) => Any(_folders, name);

    public bool ExcludesFile(string name)
    {
        if (Any(_files, name)) return true;

        var extension = Path.GetExtension(name);
        return extension.Length > 0 && Any(_extensions, extension);
    }

    public static string[] Split(string? text) => (text ?? string.Empty)
        .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();

    private static bool Any(string[] patterns, string value)
    {
        foreach (var pattern in patterns)
        {
            if (Matches(pattern, value)) return true;
        }
        return false;
    }

    private static bool Matches(string pattern, string value)
    {
        if (pattern.IndexOfAny(['*', '?']) < 0)
        {
            return string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase);
        }
        return Wildcard(pattern.AsSpan(), value.AsSpan());
    }

    private static bool Wildcard(ReadOnlySpan<char> pattern, ReadOnlySpan<char> value)
    {
        int p = 0;
        int v = 0;
        int star = -1;
        int mark = 0;

        while (v < value.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || Same(pattern[p], value[v])))
            {
                p++;
                v++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = v;
            }
            else if (star >= 0)
            {
                p = star + 1;
                v = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }

    private static bool Same(char a, char b) => char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
}
