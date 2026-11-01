namespace Optimizer.Main.Inspect;

internal static class GifAnimation
{
    private const int Budget = 4 * 1024 * 1024;

    public static bool IsAnimated(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var length = (int)Math.Min(stream.Length, Budget);
            var data = new byte[length];
            stream.ReadExactly(data);
            return Scan(data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool Scan(byte[] data)
    {
        var cursor = 0;
        if (!Take(data, ref cursor, 13, out var header)) return false;
        if (header[0] != (byte)'G' || header[1] != (byte)'I' || header[2] != (byte)'F') return false;
        if (!Skip(data, ref cursor, ColorTable(header[10]))) return false;

        var frames = 0;
        while (cursor < data.Length)
        {
            var block = data[cursor++];
            if (block == 0x3B) return false;

            if (block == 0x21)
            {
                if (cursor >= data.Length) return false;
                var label = data[cursor++];
                if (label == 0xFF)
                {
                    if (cursor >= data.Length) return false;
                    var size = data[cursor++];
                    if (cursor + size > data.Length) return false;
                    if (size >= 8 &&
                        data[cursor] == (byte)'N' && data[cursor + 1] == (byte)'E' &&
                        data[cursor + 2] == (byte)'T' && data[cursor + 3] == (byte)'S' &&
                        data[cursor + 4] == (byte)'C' && data[cursor + 5] == (byte)'A' &&
                        data[cursor + 6] == (byte)'P' && data[cursor + 7] == (byte)'E')
                    {
                        return true;
                    }
                    cursor += size;
                    if (!SkipSub(data, ref cursor)) return false;
                }
                else if (!SkipSub(data, ref cursor))
                {
                    return false;
                }
                continue;
            }

            if (block == 0x2C)
            {
                if (++frames >= 2) return true;
                if (!Take(data, ref cursor, 9, out var descriptor)) return false;
                if (!Skip(data, ref cursor, ColorTable(descriptor[8]))) return false;
                if (cursor >= data.Length) return false;
                cursor++;
                if (!SkipSub(data, ref cursor)) return false;
                continue;
            }

            return false;
        }

        return false;
    }

    private static int ColorTable(byte packed)
    {
        if ((packed & 0x80) == 0) return 0;
        return 3 * (1 << ((packed & 0x07) + 1));
    }

    private static bool Take(byte[] data, ref int cursor, int count, out byte[] slice)
    {
        slice = [];
        if (count < 0 || cursor + count > data.Length) return false;
        slice = data[cursor..(cursor + count)];
        cursor += count;
        return true;
    }

    private static bool Skip(byte[] data, ref int cursor, int count)
    {
        if (count < 0 || cursor + count > data.Length) return false;
        cursor += count;
        return true;
    }

    private static bool SkipSub(byte[] data, ref int cursor)
    {
        while (cursor < data.Length)
        {
            var length = data[cursor++];
            if (length == 0) return true;
            if (!Skip(data, ref cursor, length)) return false;
        }
        return false;
    }
}
