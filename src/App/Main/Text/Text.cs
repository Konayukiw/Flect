using System.Text;
using UtfUnknown;

namespace Optimizer.Main;

internal static class Text
{
    public static string Read(string path) => Read(path, out _);

    public static string Read(string path, out Encoding encoding)
    {
        var detected = CharsetDetector.DetectFromFile(path).Detected?.Encoding ?? Encoding.UTF8;
        using var reader = new StreamReader(path, detected, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        encoding = reader.CurrentEncoding;
        return text;
    }
}
