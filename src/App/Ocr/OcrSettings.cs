namespace Flect.Ocr;

internal sealed class OcrSettings
{
    public string Language { get; set; } = string.Empty;
    public OcrOutput Output { get; set; } = OcrOutput.TextFile;
    public TextEncodingChoice Encoding { get; set; } = TextEncodingChoice.Utf8Bom;
    public bool SuppressLanguageNotice { get; set; }
}
