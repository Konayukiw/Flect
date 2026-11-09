using System.Runtime.InteropServices;
using System.Text;
using Flect.ExplorerPreview;
using Flect.Inspect;

namespace Evals.Preview;

public sealed class PreviewTests
{
    private static readonly string Library = Path.Combine(Paths.Repository, "build", "preview", "Release", "FlectPreview.dll");

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SniffFunction(byte[] data, nuint size, string name, StringBuilder output, nuint capacity);

    public static TheoryData<string, byte[]> SharedSamples()
    {
        var data = new TheoryData<string, byte[]>
        {
            { "a.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
            { "a.jpg", [0xFF, 0xD8, 0xFF, 0xE0] },
            { "a.gif", Ascii("GIF89a") },
            { "a.webp", Ascii("RIFF\0\0\0\0WEBPVP8 ") },
            { "a.wav", Ascii("RIFF\0\0\0\0WAVEfmt ") },
            { "a.avi", Ascii("RIFF\0\0\0\0AVI LIST") },
            { "a.aif", Ascii("FORM\0\0\0\0AIFFCOMM") },
            { "a.mp4", Ascii("\0\0\0\u0020ftypisom") },
            { "a.heic", Ascii("\0\0\0\u0020ftypheic") },
            { "a.avif", Ascii("\0\0\0\u0020ftypavif") },
            { "a.mov", Ascii("\0\0\0\u0020ftypqt  ") },
            { "a.m4a", Ascii("\0\0\0\u0020ftypM4A ") },
            { "a.mkv", [0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x42, 0x82, 0x88, .. Ascii("matroska")] },
            { "a.webm", [0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x42, 0x82, 0x84, .. Ascii("webm")] },
            { "a.flv", Ascii("FLV\u0001") },
            { "a.ico", [0x00, 0x00, 0x01, 0x00, 0x01, 0x00] },
            { "a.pdf", Ascii("%PDF-1.7") },
            { "a.zip", [0x50, 0x4B, 0x03, 0x04] },
            { "a.7z", [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C] },
            { "a.rar", Ascii("Rar!\u001A\u0007") },
            { "a.gz", [0x1F, 0x8B, 0x08] },
            { "a.tar.gz", [0x1F, 0x8B, 0x08] },
            { "a.mp3", Ascii("ID3\u0004") },
            { "b.mp3", [0xFF, 0xFB, 0x90, 0x64] },
            { "a.aac", [0xFF, 0xF1, 0x50, 0x80] },
            { "a.ogg", Ascii("OggS\0\u0002") },
            { "a.flac", Ascii("fLaC\0\0\0\u0022") },
            { "a.wma", [0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C] },
            { "a.svg", Ascii("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>") },
            { "b.svg", Ascii("<?xml version=\"1.0\"?>\n<svg></svg>") },
            { "a.txt", Ascii("plain text with\nsome lines\n") },
            { "a.bin", [0x00, 0x01, 0x02, 0x00, 0x05, 0x06] },
            { "empty.txt", [] },
        };

        var tar = new byte[512];
        Encoding.ASCII.GetBytes("ustar").CopyTo(tar, 257);
        data.Add("a.tar", tar);

        var exe = new byte[0x100];
        exe[0] = (byte)'M';
        exe[1] = (byte)'Z';
        exe[0x3C] = 0x80;
        exe[0x80] = (byte)'P';
        exe[0x81] = (byte)'E';
        data.Add("a.exe", exe);

        byte[] link = [0x4C, 0, 0, 0, 0x01, 0x14, 0x02, 0, 0, 0, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46, 0, 0, 0, 0];
        data.Add("a.lnk", link);
        return data;
    }

    [Theory]
    [MemberData(nameof(SharedSamples))]
    public void NativeSniffAgreesWithAnalyze(string name, byte[] header)
    {
        var expected = Signature.Of(header, name);
        var native = Native(header, name);
        Assert.Equal(expected.Kind, native[0]);
        Assert.Equal(expected.Family, native[1]);
    }

    [Theory]
    [InlineData("a.ttf", new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x10 }, "font")]
    [InlineData("a.otf", new byte[] { 0x4F, 0x54, 0x54, 0x4F, 0x00, 0x10 }, "font")]
    [InlineData("a.woff2", new byte[] { 0x77, 0x4F, 0x46, 0x32, 0x00, 0x01 }, "font")]
    public void NativeSniffRecognizesFonts(string name, byte[] header, string family)
    {
        Assert.Equal(family, Native(header, name)[1]);
    }

    [Fact]
    public void MpegTransportStreamsAreToldApartFromTypeScript()
    {
        var packets = new byte[188 * 3 + 1];
        packets[0] = packets[188] = packets[376] = 0x47;
        Assert.Equal("MPEG-TS", Native(packets, "a.ts")[0]);
        Assert.Equal("mpegts", Native(packets, "a.ts")[2]);

        var source = Ascii("export const answer: number = 42;\n");
        Assert.Equal("TEXT", Native(source, "a.ts")[0]);
    }

    [Fact]
    public void PlaylistsDisguisedAsVideoNeverReachFfmpeg()
    {
        var native = Native(Ascii("#EXTM3U\n#EXTINF:1,\nfile:///C:/Windows/win.ini\n"), "a.mp4");
        Assert.Equal("text", native[1]);
        Assert.Equal(string.Empty, native[2]);
    }

    [Fact]
    public void ExtensionTableCoversEveryKindAndLeavesShellFormatsOut()
    {
        var extensions = PreviewAssociations.Extensions;
        Assert.Equal(extensions.Count, extensions.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var kinds = extensions.Select(item => item.Kind).ToHashSet();
        foreach (var kind in new[] { "text", "html", "markdown", "svg", "pdf", "font", "video", "audio" }) Assert.Contains(kind, kinds);

        var names = extensions.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { ".exe", ".dll", ".scr", ".msi", ".lnk", ".url", ".library-ms", ".searchconnector-ms", ".hta", ".cpl" })
        {
            Assert.DoesNotContain(name, names);
        }
    }

    private static byte[] Ascii(string text) => Encoding.Latin1.GetBytes(text);

    private static string[] Native(byte[] header, string name)
    {
        if (!File.Exists(Library)) Assert.Skip("FlectPreview.dll has not been built.");
        var module = NativeLibrary.Load(Library);
        var sniff = Marshal.GetDelegateForFunctionPointer<SniffFunction>(NativeLibrary.GetExport(module, "FlectPreviewSniff"));
        var output = new StringBuilder(128);
        Assert.Equal(1, sniff(header, (nuint)header.Length, name, output, (nuint)output.Capacity));
        return output.ToString().Split('|');
    }
}
