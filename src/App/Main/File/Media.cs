using System.Text;

namespace Optimizer.Main.Inspect;

internal readonly record struct MagicHit(string Kind, string Family);

internal static class Magic
{
    public static MagicHit Of(ReadOnlySpan<byte> header, string fileName)
    {
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return new("PNG", "image");
        }
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new("JPEG", "image");
        }
        if (header.Length >= 6 && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F')
        {
            return new("GIF", "image");
        }
        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F')
        {
            var form = Encoding.ASCII.GetString(header.Slice(8, 4));
            if (form == "WEBP") return new("WEBP", "image");
            if (form == "WAVE") return new("WAV", "audio");
            if (form == "AVI ") return new("AVI", "video");
        }
        if (header.Length >= 12 &&
            header[0] == (byte)'F' && header[1] == (byte)'O' && header[2] == (byte)'R' && header[3] == (byte)'M')
        {
            var form = Encoding.ASCII.GetString(header.Slice(8, 4));
            if (form is "AIFF" or "AIFC") return new("AIFF", "audio");
        }
        if (header.Length >= 12 && header[4] == (byte)'f' && header[5] == (byte)'t' &&
            header[6] == (byte)'y' && header[7] == (byte)'p')
        {
            var brand = Encoding.ASCII.GetString(header.Slice(8, 4)).ToLowerInvariant();
            if (brand is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "heif" or "mif1" or "msf1")
            {
                return new("HEIC", "image");
            }
            if (brand == "avif") return new("AVIF", "image");
            if (brand is "qt  ") return new("MOV", "video");
            if (brand is "m4a " or "m4b " or "m4p ") return new("M4A", "audio");
            return new("MP4", "video");
        }
        if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
        {
            var text = Encoding.ASCII.GetString(header);
            if (text.Contains("webm", StringComparison.OrdinalIgnoreCase)) return new("WEBM", "video");
            return new("MKV", "video");
        }
        if (header.Length >= 3 && header[0] == (byte)'F' && header[1] == (byte)'L' && header[2] == (byte)'V')
        {
            return new("FLV", "video");
        }
        if (header.Length >= 4 && header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x01 && header[3] == 0x00)
        {
            return new("ICO", "image");
        }
        if (header.Length >= 5 && header[0] == (byte)'%' && header[1] == (byte)'P' &&
            header[2] == (byte)'D' && header[3] == (byte)'F')
        {
            return new("PDF", "pdf");
        }
        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
            (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07))
        {
            return new("ZIP", "archive");
        }
        if (header.Length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC &&
            header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
        {
            return new("7Z", "archive");
        }
        if (header.Length >= 6 && header[0] == (byte)'R' && header[1] == (byte)'a' && header[2] == (byte)'r' &&
            header[3] == (byte)'!')
        {
            return new("RAR", "archive");
        }
        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.EndsWith(".tar.gz", StringComparison.Ordinal) || lower.EndsWith(".tgz", StringComparison.Ordinal))
            {
                return new("TAR.GZ", "archive");
            }
            return new("GZIP", "archive");
        }
        if (header.Length >= 262)
        {
            var marker = Encoding.ASCII.GetString(header.Slice(257, 5));
            if (marker.StartsWith("ustar", StringComparison.Ordinal)) return new("TAR", "archive");
        }
        if (header.Length >= 3 && header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3')
        {
            return new("MP3", "audio");
        }
        if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0 &&
            fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return new("MP3", "audio");
        }
        if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xF6) == 0xF0 &&
            fileName.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
        {
            return new("AAC", "audio");
        }
        if (header.Length >= 4 && header[0] == (byte)'O' && header[1] == (byte)'g' &&
            header[2] == (byte)'g' && header[3] == (byte)'S')
        {
            return new("OGG", "audio");
        }
        if (header.Length >= 4 && header[0] == (byte)'f' && header[1] == (byte)'L' &&
            header[2] == (byte)'a' && header[3] == (byte)'C')
        {
            return new("FLAC", "audio");
        }
        if (header.Length >= 16 && header[0] == 0x30 && header[1] == 0x26 && header[2] == 0xB2 && header[3] == 0x75)
        {
            return new("WMA", "audio");
        }
        if (IsLink(header)) return new("LNK", "lnk");
        if (IsPe(header)) return new("PE", "pe");
        if (IsSvg(header)) return new("SVG", "image");
        if (IsText(header)) return new("TEXT", "text");
        return new(string.Empty, "binary");
    }

    private static bool IsPe(ReadOnlySpan<byte> header)
    {
        if (header.Length < 0x40 || header[0] != (byte)'M' || header[1] != (byte)'Z') return false;
        var offset = BitConverter.ToInt32(header.Slice(0x3C, 4));
        if (offset < 0 || offset > header.Length - 4) return true;
        return header[offset] == (byte)'P' && header[offset + 1] == (byte)'E' &&
               header[offset + 2] == 0 && header[offset + 3] == 0;
    }

    private static bool IsLink(ReadOnlySpan<byte> header)
    {
        if (header.Length < 20) return false;
        var size = BitConverter.ToInt32(header[..4]);
        if (size != 0x4C) return false;
        ReadOnlySpan<byte> clsid =
        [
            0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46,
        ];
        return header.Slice(4, 16).SequenceEqual(clsid);
    }

    private static bool IsSvg(ReadOnlySpan<byte> header)
    {
        var text = HeaderText(header);
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
                trimmed.Contains("<svg", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsText(ReadOnlySpan<byte> header)
    {
        if (header.Length == 0) return false;
        if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xFE) return true;
        if (header.Length >= 2 && header[0] == 0xFE && header[1] == 0xFF) return true;
        if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF) return true;

        var sample = Math.Min(header.Length, 4096);
        var printable = 0;
        for (var index = 0; index < sample; index++)
        {
            var value = header[index];
            if (value == 0) return false;
            if (value is 9 or 10 or 13 || (value >= 32 && value < 127)) printable++;
            else if (value >= 128) printable++;
        }
        return printable >= sample * 0.90;
    }

    private static string HeaderText(ReadOnlySpan<byte> header)
    {
        var length = Math.Min(header.Length, 2048);
        return Encoding.UTF8.GetString(header[..length]);
    }
}
