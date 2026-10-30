using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Optimizer.Main.Inspect;

internal static class ConfirmFileSystem
{
    public static void Add(FactBag bag, FileInfo file)
    {
        bag.Add("dialog.fileAnalyze.path", file.FullName);
        bag.Add("dialog.fileAnalyze.size", Size(file.Length));
        var allocated = Allocation(file.FullName);
        if (allocated is not null) bag.Add("dialog.fileAnalyze.sizeOnDisk", Size(allocated.Value));
        bag.Add("dialog.fileAnalyze.created", file.CreationTime.ToString("G"));
        bag.Add("dialog.fileAnalyze.modified", file.LastWriteTime.ToString("G"));
        bag.Add("dialog.fileAnalyze.attributes", Attributes(file.Attributes));
        if (file.LinkTarget is not null) bag.Add("dialog.fileAnalyze.linkTarget", file.LinkTarget);

        var streams = Streams(file.FullName);
        if (streams.Count > 0)
        {
            bag.Add("dialog.fileAnalyze.streams",
                    string.Join("\n", streams.Select(stream => $"{stream.Name}  {Formatting.Bytes(stream.Size)}")));
        }

        var zone = Zone(file.FullName);
        if (zone is not null)
        {
            bag.Add("dialog.fileAnalyze.zone", zone.Value.Name);
            bag.Add("dialog.fileAnalyze.zoneUrl", zone.Value.Url);
        }
    }

    private static string Size(long bytes) =>
        $"{Formatting.Bytes(bytes)} ({bytes.ToString("N0")} {Loc.T("common.bytes")})";

    private static string Attributes(FileAttributes attributes)
    {
        var names = new List<string>();
        void Add(FileAttributes flag, string key)
        {
            if (attributes.HasFlag(flag)) names.Add(Loc.T(key));
        }

        Add(FileAttributes.ReadOnly, "dialog.fileAnalyze.attr.readOnly");
        Add(FileAttributes.Hidden, "dialog.fileAnalyze.attr.hidden");
        Add(FileAttributes.System, "dialog.fileAnalyze.attr.system");
        Add(FileAttributes.Archive, "dialog.fileAnalyze.attr.archive");
        Add(FileAttributes.Compressed, "dialog.fileAnalyze.attr.compressed");
        Add(FileAttributes.Encrypted, "dialog.fileAnalyze.attr.encrypted");
        Add(FileAttributes.SparseFile, "dialog.fileAnalyze.attr.sparse");
        Add(FileAttributes.ReparsePoint, "dialog.fileAnalyze.attr.reparse");
        Add(FileAttributes.Temporary, "dialog.fileAnalyze.attr.temporary");
        return names.Count == 0 ? Loc.T("dialog.fileAnalyze.none") : string.Join(", ", names);
    }

    private static long? Allocation(string path)
    {
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
                                                FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandleEx(handle, 1, out var info, (uint)Marshal.SizeOf<FileStandardInfo>()))
            {
                return null;
            }
            return info.AllocationSize;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static List<(string Name, long Size)> Streams(string path)
    {
        var found = new List<(string Name, long Size)>();
        var handle = FindFirstStreamW(path, 0, out var data, 0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return found;
        }
        try
        {
            do
            {
                var name = data.Name;
                if (name.Equals("::$DATA", StringComparison.OrdinalIgnoreCase)) continue;
                var trimmed = name.TrimStart(':');
                var split = trimmed.IndexOf(':');
                if (split > 0) trimmed = trimmed[..split];
                if (trimmed.Equals("Zone.Identifier", StringComparison.OrdinalIgnoreCase)) continue;
                found.Add((trimmed, data.StreamSize));
            }
            while (FindNextStreamW(handle, out data));
        }
        finally
        {
            handle.Dispose();
        }
        return found;
    }

    private static (string Name, string? Url)? Zone(string path)
    {
        try
        {
            var text = File.ReadAllText(path + ":Zone.Identifier");
            string? id = null;
            string? url = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                var split = line.IndexOf('=');
                if (split <= 0) continue;
                var key = line[..split].Trim();
                var value = line[(split + 1)..].Trim();
                if (key.Equals("ZoneId", StringComparison.OrdinalIgnoreCase)) id = value;
                else if (key.Equals("HostUrl", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("ReferrerUrl", StringComparison.OrdinalIgnoreCase))
                {
                    url ??= value;
                }
            }

            if (id is null) return null;
            var name = id switch
            {
                "0" => Loc.T("dialog.fileAnalyze.zone0"),
                "1" => Loc.T("dialog.fileAnalyze.zone1"),
                "2" => Loc.T("dialog.fileAnalyze.zone2"),
                "3" => Loc.T("dialog.fileAnalyze.zone3"),
                "4" => Loc.T("dialog.fileAnalyze.zone4"),
                _ => id,
            };
            return (name, url);
        }
        catch (IOException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileStandardInfo
    {
        public long AllocationSize;
        public long EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending;
        public byte Directory;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindStreamData
    {
        public long StreamSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string Name;
    }

    private sealed class FindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public FindHandle() : base(true) { }

        protected override bool ReleaseHandle() => FindClose(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file, int fileInformationClass, out FileStandardInfo info, uint bufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern FindHandle FindFirstStreamW(
        string fileName, int infoLevel, out FindStreamData data, int flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool FindNextStreamW(FindHandle handle, out FindStreamData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr handle);
}
