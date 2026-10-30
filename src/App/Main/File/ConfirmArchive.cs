using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Optimizer.Main.Inspect;

internal static class ConfirmArchive
{
    private const int TopCount = 15;

    public static async Task<ProbeResult?> ReadAsync(string path, string kind, CancellationToken token)
    {
        if (kind is "ZIP" or "JAR" || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            var zip = ReadZip(path, token);
            if (zip is not null) return zip;
        }

        return await ReadSevenZipAsync(path, kind, token);
    }

    private static ProbeResult? ReadZip(string path, CancellationToken token)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var files = 0;
            var folders = 0;
            long unpacked = 0;
            long packed = 0;
            var classes = 0;
            var others = 0;
            var signed = false;
            int? minClass = null;
            int? maxClass = null;
            var top = new List<(string Path, long Size)>();
            string? mainClass = null;
            string? title = null;
            string? version = null;
            var manifest = false;

            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith('/'))
                {
                    folders++;
                    continue;
                }

                files++;
                unpacked += entry.Length;
                packed += entry.CompressedLength;
                Consider(top, name, entry.Length);

                if (name.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase))
                {
                    manifest = true;
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    (mainClass, title, version) = Manifest(reader.ReadToEnd());
                }
                else if (name.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
                {
                    classes++;
                    var major = ClassMajor(entry);
                    if (major is not null)
                    {
                        minClass = minClass is null ? major : Math.Min(minClass.Value, major.Value);
                        maxClass = maxClass is null ? major : Math.Max(maxClass.Value, major.Value);
                    }
                }
                else
                {
                    others++;
                }

                if (name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".SF", StringComparison.OrdinalIgnoreCase))
                {
                    signed = true;
                }
            }

            var encrypted = CentralDirectoryEncrypted(path);
            var bag = new FactBag();
            var detected = manifest || path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ? "JAR" : "ZIP";
            bag.Add("dialog.fileAnalyze.format", detected);
            bag.Add("dialog.fileAnalyze.encrypted", encrypted is null ? null : Kinds.YesNo(encrypted.Value));
            bag.Add("dialog.fileAnalyze.files", files.ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.folders", folders.ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.uncompressed", Formatting.Bytes(unpacked));
            bag.Add("dialog.fileAnalyze.packed", Formatting.Bytes(packed));
            bag.Add("dialog.fileAnalyze.ratio", Ratio(packed, unpacked));
            Largest(bag, top);

            var sections = new List<FileSection>();
            var archiveSection = bag.Section("dialog.fileAnalyze.archive");
            if (archiveSection is not null) sections.Add(archiveSection);

            if (detected == "JAR")
            {
                var jar = new FactBag();
                jar.Add("dialog.fileAnalyze.manifest", Kinds.YesNo(manifest));
                jar.Add("dialog.fileAnalyze.mainClass", mainClass);
                jar.Add("dialog.fileAnalyze.metaTitle", title);
                jar.Add("dialog.fileAnalyze.implVersion", version);
                jar.Add("dialog.fileAnalyze.classes", classes.ToString(CultureInfo.InvariantCulture));
                jar.Add("dialog.fileAnalyze.otherEntries", others.ToString(CultureInfo.InvariantCulture));
                jar.Add("dialog.fileAnalyze.bytecode", Bytecode(minClass, maxClass));
                jar.Add("dialog.fileAnalyze.signed", Kinds.YesNo(signed));
                var jarSection = jar.Section("dialog.fileAnalyze.jar");
                if (jarSection is not null) sections.Add(jarSection);
            }

            return new ProbeResult { Kind = detected, Sections = sections };
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<ProbeResult?> ReadSevenZipAsync(string path, string kind, CancellationToken token)
    {
        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunOrThrowAsync(Tools.SevenZipFull, ["l", "-slt", path], token);
        }
        catch (Exception ex) when (ex is FileNotFoundException or ProcessFailure)
        {
            return null;
        }

        var blocks = Blocks(result.StandardOutput);
        string? format = null;
        string? comment = null;
        var encrypted = false;
        var files = 0;
        var folders = 0;
        long unpacked = 0;
        long packed = 0;
        var top = new List<(string Path, long Size)>();

        foreach (var block in blocks)
        {
            if (block.TryGetValue("Type", out var type) && !block.ContainsKey("Folder"))
            {
                format = type.ToUpperInvariant();
                if (block.TryGetValue("Comment", out var text) && text.Length > 0) comment = text;
                if (Flag(block, "Encrypted")) encrypted = true;
                continue;
            }

            if (!block.TryGetValue("Path", out var entry)) continue;
            var directory = Flag(block, "Folder");
            if (Flag(block, "Encrypted")) encrypted = true;
            if (directory)
            {
                folders++;
                continue;
            }

            files++;
            var size = Long(block, "Size");
            unpacked += size;
            packed += Long(block, "Packed Size");
            Consider(top, entry, size);
        }

        if (files == 0 && folders == 0 && format is null) return null;
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.format", format ?? kind);
        bag.Add("dialog.fileAnalyze.encrypted", Kinds.YesNo(encrypted));
        bag.Add("dialog.fileAnalyze.files", files.ToString(CultureInfo.InvariantCulture));
        bag.Add("dialog.fileAnalyze.folders", folders.ToString(CultureInfo.InvariantCulture));
        bag.Add("dialog.fileAnalyze.uncompressed", Formatting.Bytes(unpacked));
        bag.Add("dialog.fileAnalyze.packed", Formatting.Bytes(packed));
        bag.Add("dialog.fileAnalyze.ratio", Ratio(packed, unpacked));
        bag.Add("dialog.fileAnalyze.comment", comment);
        Largest(bag, top);
        var section = bag.Section("dialog.fileAnalyze.archive");
        return new ProbeResult
        {
            Kind = format ?? (kind.Length == 0 ? "ARCHIVE" : kind),
            Sections = section is null ? [] : [section],
        };
    }

    private static void Largest(FactBag bag, List<(string Path, long Size)> top)
    {
        if (top.Count == 0) return;
        var text = string.Join("\n", top.OrderByDescending(item => item.Size)
                                         .Select(item => $"{Formatting.Bytes(item.Size)}  {item.Path}"));
        bag.Add("dialog.fileAnalyze.largest", text);
    }

    private static void Consider(List<(string Path, long Size)> top, string path, long size)
    {
        if (top.Count < TopCount)
        {
            top.Add((path, size));
            return;
        }

        var index = 0;
        for (var item = 1; item < top.Count; item++)
        {
            if (top[item].Size < top[index].Size) index = item;
        }
        if (size > top[index].Size) top[index] = (path, size);
    }

    private static string? Ratio(long packed, long unpacked)
    {
        if (unpacked <= 0) return null;
        return ((double)packed / unpacked).ToString("0.0%", CultureInfo.InvariantCulture);
    }

    private static int? ClassMajor(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var header = new byte[8];
            if (stream.ReadAtLeast(header, 8, throwOnEndOfStream: false) < 8) return null;
            if (header[0] != 0xCA || header[1] != 0xFE || header[2] != 0xBA || header[3] != 0xBE) return null;
            return (header[6] << 8) | header[7];
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static (string? Main, string? Title, string? Version) Manifest(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        var value = new StringBuilder();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(' ') && key is not null)
            {
                value.Append(line.Trim());
                continue;
            }

            if (key is not null) map[key] = value.ToString();
            var split = line.IndexOf(':');
            if (split <= 0)
            {
                key = null;
                value.Clear();
                continue;
            }

            key = line[..split].Trim();
            value.Clear();
            value.Append(line[(split + 1)..].Trim());
        }
        if (key is not null) map[key] = value.ToString();

        map.TryGetValue("Main-Class", out var main);
        map.TryGetValue("Implementation-Title", out var title);
        map.TryGetValue("Implementation-Version", out var version);
        return (main, title, version);
    }

    private static string? Bytecode(int? min, int? max)
    {
        if (min is null || max is null) return null;
        var low = Java(min.Value);
        var high = Java(max.Value);
        return min == max ? $"{min} ({low})" : $"{min}-{max} ({low} - {high})";
    }

    private static string Java(int major) => major >= 45 ? "Java " + (major - 44) : "major " + major;

    private static bool? CentralDirectoryEncrypted(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 22) return null;
            var tail = (int)Math.Min(stream.Length, 22 + 65535);
            stream.Position = stream.Length - tail;
            var buffer = new byte[tail];
            stream.ReadExactly(buffer);
            var end = -1;
            for (var index = buffer.Length - 22; index >= 0; index--)
            {
                if (buffer[index] == 0x50 && buffer[index + 1] == 0x4B &&
                    buffer[index + 2] == 0x05 && buffer[index + 3] == 0x06)
                {
                    end = index;
                    break;
                }
            }
            if (end < 0) return null;
            var offset = BitConverter.ToUInt32(buffer, end + 16);
            var count = BitConverter.ToUInt16(buffer, end + 10);
            if (offset == uint.MaxValue) return null;
            stream.Position = offset;
            for (var entry = 0; entry < count; entry++)
            {
                var header = new byte[46];
                if (stream.ReadAtLeast(header, 46, throwOnEndOfStream: false) < 46) return null;
                if (header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x01 || header[3] != 0x02) return null;
                var flags = BitConverter.ToUInt16(header, 8);
                if ((flags & 1) != 0) return true;
                var name = BitConverter.ToUInt16(header, 28);
                var extra = BitConverter.ToUInt16(header, 30);
                var comment = BitConverter.ToUInt16(header, 32);
                stream.Position += name + extra + comment;
            }
            return false;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }

    private static List<Dictionary<string, string>> Blocks(string output)
    {
        var blocks = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("----", StringComparison.Ordinal))
            {
                if (current is { Count: > 0 }) blocks.Add(current);
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var split = line.IndexOf(" = ", StringComparison.Ordinal);
            if (split <= 0) continue;
            current ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            current[line[..split].Trim()] = line[(split + 3)..].Trim();
        }
        if (current is { Count: > 0 }) blocks.Add(current);
        return blocks;
    }

    private static bool Flag(Dictionary<string, string> block, string key) =>
        block.TryGetValue(key, out var value) && value == "+";

    private static long Long(Dictionary<string, string> block, string key) =>
        block.TryGetValue(key, out var value) &&
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
}
