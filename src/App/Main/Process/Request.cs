using System.Text;

namespace Optimizer.Main.Process;

internal sealed class Request
{
    private readonly Dictionary<string, string> _parameters;

    private Request(string domain, string action, Dictionary<string, string> parameters,
                       IReadOnlyList<string> paths)
    {
        Domain = domain;
        Action = action;
        _parameters = parameters;
        Paths = paths;
    }

    public string Domain { get; }
    public string Action { get; }
    public IReadOnlyList<string> Paths { get; }

    public string Id => $"{Domain}.{Action}";

    public string? this[string key] => _parameters.GetValueOrDefault(key);

    public static Request? Parse(string[] args)
    {
        string? verb = null;
        string? input = null;

        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--task": verb = args[i + 1]; break;
                case "--input": input = args[i + 1]; break;
            }
        }

        if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(input)) return null;

        var paths = ReadSelection(input);
        if (paths.Count == 0) return null;

        var query = verb.Split('?', 2);
        var name = query[0].Split('.', 2);
        if (name.Length != 2) return null;

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (query.Length == 2)
        {
            foreach (var pair in query[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                parameters[kv[0]] = kv.Length == 2 ? kv[1] : string.Empty;
            }
        }

        return new Request(name[0], name[1], parameters, paths);
    }

    private static List<string> ReadSelection(string listFile)
    {
        var paths = new List<string>();
        try
        {
            foreach (var line in File.ReadAllLines(listFile, Encoding.UTF8))
            {
                var path = line.Trim();
                if (path.Length > 0) paths.Add(path);
            }
        }
        catch (IOException)
        {
            return paths;
        }
        finally
        {
            try { File.Delete(listFile); } catch (IOException) {}
        }
        return paths;
    }
}