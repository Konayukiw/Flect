using System.Text;
using UtfUnknown;

namespace Optimizer.Main;

/// <summary>
/// Reading text off disk without being told what encoding it is in, which is the
/// normal situation for a file someone right-clicked.
/// </summary>
internal static class TextFile
{
    public static string Read(string path) => Read(path, out _);

    /// <param name="encoding">What the file turned out to be, so a caller rewriting
    /// it in place can put it back the same way.</param>
    public static string Read(string path, out Encoding encoding)
    {
        var detected = CharsetDetector.DetectFromFile(path).Detected?.Encoding ?? Encoding.UTF8;
        using var reader = new StreamReader(path, detected, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        encoding = reader.CurrentEncoding;
        return text;
    }
}
