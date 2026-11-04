using System.Diagnostics;
using System.Globalization;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Flect.Ocr;

internal sealed record OcrLanguage(string Tag, string Name)
{
    public string Display => $"{Name} ({Tag})";
}
