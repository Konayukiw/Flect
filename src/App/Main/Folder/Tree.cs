using System.Text;

namespace Optimizer.Main.Folder;

internal static class TreeRenderer
{
    private const int MaxEntries = 200000;

    public static string Render(IReadOnlyList<string> roots, TreeSettings settings,
                                ITaskProgress progress)
    {
        var exclusions = ExclusionFilter.From(settings.Exclusions);
        var text = new StringBuilder();
        int budget = MaxEntries;

        foreach (var root in roots)
        {
            progress.Token.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) continue;

            progress.Status(root);
            if (text.Length > 0) text.AppendLine().AppendLine();

            text.AppendLine(Path.GetFullPath(root));
            Branch(new DirectoryInfo(root), string.Empty, text, settings, exclusions, progress,
                   ref budget);
        }
        return text.ToString();
    }

    private static void Branch(DirectoryInfo directory, string prefix, StringBuilder text,
                               TreeSettings settings, ExclusionFilter exclusions,
                               ITaskProgress progress, ref int budget)
    {
        progress.Token.ThrowIfCancellationRequested();

        var folders = Children(directory, exclusions);
        var files = settings.FoldersOnly ? [] : Files(directory, exclusions);

        for (int index = 0; index < folders.Count; index++)
        {
            if (budget-- <= 0) return;

            bool last = index == folders.Count - 1 && files.Count == 0;
            text.AppendLine(prefix + (last ? "└───" : "├───") + folders[index].Name);
            Branch(folders[index], prefix + (last ? "    " : "│   "), text, settings, exclusions,
                   progress, ref budget);
        }

        for (int index = 0; index < files.Count; index++)
        {
            if (budget-- <= 0) return;

            bool last = index == files.Count - 1;
            text.AppendLine(prefix + (last ? "└───" : "├───") + files[index]);
        }
    }

    private static List<DirectoryInfo> Children(DirectoryInfo directory, ExclusionFilter exclusions)
    {
        try
        {
            return directory.EnumerateDirectories()
                .Where(child => (child.Attributes & FileAttributes.ReparsePoint) == 0)
                .Where(child => !exclusions.ExcludesFolder(child.Name))
                .OrderBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static List<string> Files(DirectoryInfo directory, ExclusionFilter exclusions)
    {
        try
        {
            return directory.EnumerateFiles()
                .Select(file => file.Name)
                .Where(name => !exclusions.ExcludesFile(name))
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
