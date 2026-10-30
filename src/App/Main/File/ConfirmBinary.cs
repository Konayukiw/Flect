using System.Text;

namespace Optimizer.Main.Inspect;

internal static class ConfirmBinary
{
    public static ProbeResult? Pe(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 0x40) return null;
        if (reader.ReadUInt16() != 0x5A4D) return null;
        stream.Position = 0x3C;
        var offset = reader.ReadInt32();
        if (offset <= 0 || offset > stream.Length - 24) return null;
        stream.Position = offset;
        if (reader.ReadUInt32() != 0x00004550) return null;
        var machine = reader.ReadUInt16();
        stream.Position = offset + 20;
        var optionalSize = reader.ReadUInt16();
        var characteristics = reader.ReadUInt16();
        var optional = stream.Position;
        if (optionalSize < 2 || optional + 2 > stream.Length) return null;
        var magic = reader.ReadUInt16();
        var plus = magic == 0x20B;
        ushort subsystem = 0;
        if (optional + 70 <= stream.Length && optionalSize >= 70)
        {
            stream.Position = optional + 68;
            subsystem = reader.ReadUInt16();
        }

        uint certificate = 0;
        uint runtime = 0;
        var directory = plus ? 112 : 96;
        if (optionalSize >= directory + 15 * 8 && optional + directory + 15 * 8 <= stream.Length)
        {
            stream.Position = optional + directory + 4 * 8 + 4;
            certificate = reader.ReadUInt32();
            stream.Position = optional + directory + 14 * 8 + 4;
            runtime = reader.ReadUInt32();
        }

        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.machine", Machine(machine));
        bag.Add("dialog.fileAnalyze.subsystem", Subsystem(subsystem));
        bag.Add("dialog.fileAnalyze.runtime", runtime > 0 ? Loc.T("dialog.fileAnalyze.dotnet") : Loc.T("dialog.fileAnalyze.native"));
        bag.Add("dialog.fileAnalyze.signature", Kinds.YesNo(certificate > 0));
        if ((characteristics & 0x2000) != 0) bag.Add("dialog.fileAnalyze.nativeLibrary", Kinds.YesNo(true));
        var section = bag.Section("dialog.fileAnalyze.executable");
        return new ProbeResult
        {
            Kind = "PE",
            Executable = true,
            Sections = section is null ? [] : [section],
        };
    }

    public static ProbeResult? Link(string path)
    {
        var length = new FileInfo(path).Length;
        if (length < 0x4C || length > 1024 * 1024) return null;
        var data = File.ReadAllBytes(path);
        if (BitConverter.ToUInt32(data, 0) != 0x4C) return null;
        var flags = BitConverter.ToUInt32(data, 0x14);
        var cursor = 0x4C;
        if ((flags & 0x1) != 0)
        {
            if (cursor + 2 > data.Length) return null;
            var list = BitConverter.ToUInt16(data, cursor);
            cursor += 2 + list;
        }

        string? target = null;
        if ((flags & 0x2) != 0)
        {
            if (cursor + 4 > data.Length) return null;
            var infoSize = BitConverter.ToInt32(data, cursor);
            if (infoSize < 0 || cursor + infoSize > data.Length) return null;
            target = LocalPath(data, cursor);
            cursor += infoSize;
        }

        string? relative = null;
        string? directory = null;
        string? arguments = null;
        if ((flags & 0x4) != 0) ReadString(data, ref cursor);
        if ((flags & 0x8) != 0) relative = ReadString(data, ref cursor);
        if ((flags & 0x10) != 0) directory = ReadString(data, ref cursor);
        if ((flags & 0x20) != 0) arguments = ReadString(data, ref cursor);

        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.target", string.IsNullOrWhiteSpace(target) ? relative : target);
        bag.Add("dialog.fileAnalyze.arguments", arguments);
        bag.Add("dialog.fileAnalyze.workingDirectory", directory);
        var section = bag.Section("dialog.fileAnalyze.shortcut");
        return section is null
            ? null
            : new ProbeResult { Kind = "LNK", Executable = true, Sections = [section] };
    }

    private static string? LocalPath(byte[] data, int start)
    {
        if (start + 28 > data.Length) return null;
        var headerSize = BitConverter.ToUInt32(data, start + 4);
        var flags = BitConverter.ToUInt32(data, start + 8);
        if ((flags & 0x1) == 0) return null;
        var offset = BitConverter.ToUInt32(data, start + 16);
        var unicode = headerSize >= 0x24 ? BitConverter.ToUInt32(data, start + 28) : 0;
        if (unicode != 0) return ReadZ(data, start + (int)unicode, unicode: true);
        if (offset == 0) return null;
        return ReadZ(data, start + (int)offset, unicode: false);
    }

    private static string? ReadZ(byte[] data, int start, bool unicode)
    {
        if (start < 0 || start >= data.Length) return null;
        if (!unicode)
        {
            var end = start;
            while (end < data.Length && data[end] != 0) end++;
            return Encoding.Default.GetString(data, start, end - start);
        }

        var chars = start;
        while (chars + 1 < data.Length && (data[chars] != 0 || data[chars + 1] != 0)) chars += 2;
        return Encoding.Unicode.GetString(data, start, chars - start);
    }

    private static string? ReadString(byte[] data, ref int cursor)
    {
        if (cursor + 2 > data.Length) return null;
        var count = BitConverter.ToUInt16(data, cursor);
        cursor += 2;
        var bytes = count * 2;
        if (cursor + bytes > data.Length) return null;
        var text = Encoding.Unicode.GetString(data, cursor, bytes);
        cursor += bytes;
        return text;
    }

    private static string Machine(ushort machine) => machine switch
    {
        0x14C => "x86",
        0x8664 => "AMD64",
        0xAA64 => "ARM64",
        0x1C0 => "ARM",
        0x1C4 => "ARM Thumb-2",
        _ => "0x" + machine.ToString("X4"),
    };

    private static string Subsystem(ushort subsystem) => subsystem switch
    {
        1 => Loc.T("dialog.fileAnalyze.subsystemNative"),
        2 => Loc.T("dialog.fileAnalyze.subsystemGui"),
        3 => Loc.T("dialog.fileAnalyze.subsystemConsole"),
        5 => "OS/2",
        7 => "POSIX",
        9 => "Windows CE",
        10 => "EFI application",
        11 => "EFI boot",
        12 => "EFI runtime",
        16 => "Windows boot",
        _ => subsystem.ToString(),
    };
}
