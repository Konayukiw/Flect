using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Flect.ExplorerPreview;

internal sealed record PreviewExtension(string Name, string Kind);

internal sealed record ThirdPartyHandler(string Extension, string Handler);

internal static partial class PreviewAssociations
{
    private const string Marker = "{8895b1c6-b41f-4c1c-a562-0d564250836f}";
    private const string BackupKey = @"Software\Flect\PreviewBackup";
    private const string ClassesKey = @"Software\Classes\";
    private const string FileAssociationsKey = ClassesKey + @"SystemFileAssociations\";
    private const string ResourceName = "Flect.Preview.Extensions";
    private const string HtmlKind = "html";
    private const int ShellExtensionString = 16;
    private const int ProgIdString = 20;
    private const int AssociationChanged = 0x08000000;
    private const int BufferLength = 512;

    public static IReadOnlyList<PreviewExtension> Extensions { get; } = LoadExtensions();

    public static void Apply(PreviewSettings settings)
    {
        Revert();
        if (settings.Enabled)
        {
            var disabled = new HashSet<string>(settings.DisabledKinds, StringComparer.OrdinalIgnoreCase);
            var replace = new HashSet<string>(settings.ThirdPartyOverrides, StringComparer.OrdinalIgnoreCase);
            foreach (var extension in Extensions)
            {
                if (!disabled.Contains(extension.Kind)) ApplyOne(extension.Name, HandlerFor(extension), replace.Contains(extension.Name));
            }
        }
        Notify();
    }

    public static void Revert()
    {
        using (var backup = Registry.CurrentUser.OpenSubKey(BackupKey, writable: true))
        {
            if (backup is not null)
            {
                foreach (var key in backup.GetValueNames())
                {
                    Restore(key, backup.GetValue(key) as string ?? string.Empty);
                }
            }
        }
        Registry.CurrentUser.DeleteSubKeyTree(BackupKey, throwOnMissingSubKey: false);
        Notify();
    }

    public static IReadOnlyList<ThirdPartyHandler> FindThirdParty(PreviewSettings settings)
    {
        var replaced = new HashSet<string>(settings.ThirdPartyOverrides, StringComparer.OrdinalIgnoreCase);
        var found = new List<ThirdPartyHandler>();
        foreach (var extension in Extensions)
        {
            var current = Query(extension.Name, ShellExtensionString);
            if (current is not null && !IsFlect(current) && IsThirdParty(current))
            {
                found.Add(new ThirdPartyHandler(extension.Name, HandlerName(current)));
            }
            else if (replaced.Contains(extension.Name))
            {
                found.Add(new ThirdPartyHandler(extension.Name, Branding.Name));
            }
        }
        return found;
    }

    private static List<PreviewExtension> LoadExtensions()
    {
        using var stream = typeof(PreviewAssociations).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"{ResourceName} is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return EntryPattern().Matches(reader.ReadToEnd())
                             .Select(match => new PreviewExtension(match.Groups[1].Value, match.Groups[2].Value.ToLowerInvariant()))
                             .ToList();
    }

    [GeneratedRegex("""ENTRY\("([^"]+)",\s*(\w+),""")]
    private static partial Regex EntryPattern();

    private static string HandlerFor(PreviewExtension extension) =>
        extension.Kind == HtmlKind ? Branding.PreviewHtmlClsid : Branding.PreviewClsid;

    private static void ApplyOne(string extension, string handler, bool replaceThirdParty)
    {
        var current = Query(extension, ShellExtensionString);
        if (IsHandler(current, handler)) return;
        if (current is not null && !IsFlect(current) && IsThirdParty(current) && !replaceThirdParty) return;

        Write($@"{FileAssociationsKey}{extension}\shellex\{Marker}", handler);
        if (IsHandler(Query(extension, ShellExtensionString), handler)) return;

        var progId = Query(extension, ProgIdString);
        if (!string.IsNullOrEmpty(progId))
        {
            Write($@"{ClassesKey}{progId}\shellex\{Marker}", handler);
            if (IsHandler(Query(extension, ShellExtensionString), handler)) return;
        }

        Write($@"{ClassesKey}{extension}\shellex\{Marker}", handler);
    }

    private static void Write(string key, string handler)
    {
        using var target = Registry.CurrentUser.CreateSubKey(key);
        var previous = target.GetValue(null) as string ?? string.Empty;
        if (IsFlect(previous)) previous = string.Empty;
        using (var backup = Registry.CurrentUser.CreateSubKey(BackupKey))
        {
            if (backup.GetValue(key) is null) backup.SetValue(key, previous, RegistryValueKind.String);
        }
        target.SetValue(null, handler, RegistryValueKind.String);
    }

    private static void Restore(string key, string previous)
    {
        using (var target = Registry.CurrentUser.OpenSubKey(key, writable: true))
        {
            if (target is null || !IsFlect(target.GetValue(null) as string)) return;
            if (previous.Length > 0)
            {
                target.SetValue(null, previous, RegistryValueKind.String);
                return;
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);

        var shellex = Parent(key);
        if (!RemoveIfEmpty(shellex)) return;
        var owner = Parent(shellex);
        if (owner.StartsWith(FileAssociationsKey, StringComparison.OrdinalIgnoreCase)) RemoveIfEmpty(owner);
    }

    private static bool RemoveIfEmpty(string key)
    {
        using (var target = Registry.CurrentUser.OpenSubKey(key))
        {
            if (target is null || target.SubKeyCount != 0 || target.ValueCount != 0) return false;
        }
        Registry.CurrentUser.DeleteSubKey(key, throwOnMissingSubKey: false);
        return true;
    }

    private static string Parent(string key) => key[..key.LastIndexOf('\\')];

    private static bool IsFlect(string? clsid) =>
        IsHandler(clsid, Branding.PreviewClsid) || IsHandler(clsid, Branding.PreviewHtmlClsid);

    private static bool IsHandler(string? clsid, string handler) =>
        string.Equals(clsid, handler, StringComparison.OrdinalIgnoreCase);

    private static bool IsThirdParty(string clsid)
    {
        var path = ServerPath(clsid);
        if (path is null || !File.Exists(path)) return false;
        if (path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\", StringComparison.OrdinalIgnoreCase)) return false;
        return !path.Contains(@"\Microsoft\Edge", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ServerPath(string clsid)
    {
        using var server = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\InprocServer32");
        return server?.GetValue(null) is string value ? Environment.ExpandEnvironmentVariables(value).Trim('"') : null;
    }

    private static string HandlerName(string clsid)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}");
        if (key?.GetValue(null) is string { Length: > 0 } name) return name;
        return Path.GetFileName(ServerPath(clsid) ?? clsid);
    }

    private static string? Query(string extension, int kind)
    {
        uint length = BufferLength;
        var buffer = new StringBuilder(BufferLength);
        var extra = kind == ShellExtensionString ? Marker : null;
        return AssocQueryStringW(0, kind, extension, extra, buffer, ref length) == 0 ? buffer.ToString() : null;
    }

    private static void Notify() => SHChangeNotify(AssociationChanged, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryStringW(int flags, int kind, string association, string? extra, StringBuilder output, ref uint length);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr first, IntPtr second);
}
