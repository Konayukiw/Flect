namespace Optimizer.Main.Process;

internal sealed record MenuEntry(string Id, string LabelKey, string Label,
                                 IReadOnlyList<MenuEntry> Children)
{
    public static MenuEntry Leaf(string id, string label, string? labelKey = null) =>
        new(id, labelKey ?? "menu." + id, label, []);

    public static MenuEntry Group(string id, string label, params MenuEntry[] children) =>
        new(id, "menu." + id, label, children);
}

internal static class MenuCatalog
{
    private static IReadOnlyList<MenuEntry>? _categories;

    public static IReadOnlyList<MenuEntry> Categories => _categories ??= Build();

    public static IEnumerable<MenuEntry> Flatten(IEnumerable<MenuEntry>? entries = null)
    {
        foreach (var entry in entries ?? Categories)
        {
            yield return entry;
            foreach (var child in Flatten(entry.Children)) yield return child;
        }
    }

    private static IReadOnlyList<MenuEntry> Build() =>
    [
        MenuEntry.Group("folder", "Folder",
            MenuEntry.Leaf("folder.removeDuplicate", "Remove Duplicate"),
            MenuEntry.Leaf("folder.removeEmpty", "Remove Empty"),
            MenuEntry.Leaf("folder.tree", "Tree"),
            MenuEntry.Leaf("folder.rename", "Rename"),
            MenuEntry.Leaf("folder.analyze", "Analyze"),
            MenuEntry.Leaf("folder.compress", "Compress")),

        MenuEntry.Group("archive", "Archive",
            MenuEntry.Leaf("archive.extract", "Extract")),

        MenuEntry.Group("pdf", "PDF",
            MenuEntry.Leaf("pdf.preview", "Preview"),
            MenuEntry.Leaf("pdf.merge", "Merge"),
            MenuEntry.Leaf("pdf.convert.PNG", "to PNG", "menu.to"),
            MenuEntry.Leaf("pdf.convert.JPG", "to JPG", "menu.to"),
            MenuEntry.Leaf("pdf.convert.TXT", "to TXT", "menu.to")),

        MenuEntry.Group("image", "Image",
            MenuEntry.Group("image.resize", "Resize",
                MenuEntry.Leaf("image.resize.p1", "Preset 1", "menu.preset1"),
                MenuEntry.Leaf("image.resize.p2", "Preset 2", "menu.preset2"),
                MenuEntry.Leaf("image.resize.custom", "Custom...", "menu.custom")),
            MenuEntry.Group("image.compress", "Compress",
                MenuEntry.Leaf("image.compress.p1", "Preset 1", "menu.preset1"),
                MenuEntry.Leaf("image.compress.p2", "Preset 2", "menu.preset2"),
                MenuEntry.Leaf("image.compress.p3", "Preset 3", "menu.preset3"),
                MenuEntry.Leaf("image.compress.p4", "Preset 4", "menu.preset4"),
                MenuEntry.Leaf("image.compress.custom", "Custom...", "menu.custom")),
            MenuEntry.Group("image.rotate", "Rotate",
                MenuEntry.Leaf("image.rotate.p1", "Preset 1", "menu.preset1"),
                MenuEntry.Leaf("image.rotate.p2", "Preset 2", "menu.preset2"),
                MenuEntry.Leaf("image.rotate.p3", "Preset 3", "menu.preset3")),
             MenuEntry.Group("image.convert", "Convert",
                MenuEntry.Leaf("image.convert.PNG", "to PNG", "menu.to"),
                MenuEntry.Leaf("image.convert.JPG", "to JPG", "menu.to"),
                MenuEntry.Leaf("image.convert.WEBP", "to WEBP", "menu.to"),
                MenuEntry.Leaf("image.convert.HEIC", "to HEIC", "menu.to"),
                MenuEntry.Leaf("image.convert.ICO", "to ICO", "menu.to"),
                MenuEntry.Leaf("image.convert.GIF", "to GIF", "menu.to"),
                MenuEntry.Leaf("image.convert.SVG", "to SVG", "menu.to")),
            MenuEntry.Leaf("image.ocr", "OCR"),
            MenuEntry.Leaf("image.strip", "Remove Metadata"),
            MenuEntry.Leaf("image.chromakey", "Remove Background")),

        MenuEntry.Group("video", "Video",
            MenuEntry.Group("video.resize", "Resize",
                MenuEntry.Leaf("video.resize.p1", "Preset 1", "menu.preset1"),
                MenuEntry.Leaf("video.resize.p2", "Preset 2", "menu.preset2"),
                MenuEntry.Leaf("video.resize.custom", "Custom", "menu.custom")),
            MenuEntry.Leaf("video.trim", "Trim"),
            MenuEntry.Leaf("video.thumbnail", "Thumbnail"),
            MenuEntry.Leaf("video.subtitles", "Subtitles"),
            MenuEntry.Group("video.compress", "Compress",
                MenuEntry.Leaf("video.compress.discord", "Discord"),
                MenuEntry.Leaf("video.compress.p1", "Preset 1", "menu.preset1"),
                MenuEntry.Leaf("video.compress.p2", "Preset 2", "menu.preset2"),
                MenuEntry.Leaf("video.compress.p3", "Preset 3", "menu.preset3"),
                MenuEntry.Leaf("video.compress.custom", "Custom", "menu.custom")),
            MenuEntry.Group("video.rotate", "Rotate",
                MenuEntry.Leaf("video.rotate.p1", "Preset 1", "menu.preset1"),
                MenuEntry.Leaf("video.rotate.p2", "Preset 2", "menu.preset2"),
                MenuEntry.Leaf("video.rotate.p3", "Preset 3", "menu.preset3")),
            MenuEntry.Group("video.convert", "Convert",
                MenuEntry.Leaf("video.convert.MP4", "to MP4", "menu.to"),
                MenuEntry.Leaf("video.convert.MOV", "to MOV", "menu.to"),
                MenuEntry.Leaf("video.convert.MKV", "to MKV", "menu.to"),
                MenuEntry.Leaf("video.convert.M4A", "to M4A", "menu.to"),
                MenuEntry.Leaf("video.convert.AVI", "to AVI", "menu.to"),
                MenuEntry.Leaf("video.convert.WEBM", "to WEBM", "menu.to"),
                MenuEntry.Leaf("video.convert.FLV", "to FLV", "menu.to"),
                MenuEntry.Leaf("video.convert.GIF", "to GIF", "menu.to")),
            MenuEntry.Group("video.extractAudio", "Extract Audio",
                MenuEntry.Leaf("video.extractAudio.MP3", "to MP3", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.M4A", "to M4A", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.WAV", "to WAV", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.AIF", "to AIF", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.AIFF", "to AIFF", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.AAC", "to AAC", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.OGG", "to OGG", "menu.to"),
                MenuEntry.Leaf("video.extractAudio.WMA", "to WMA", "menu.to"))),

        MenuEntry.Group("audio", "Audio",
            MenuEntry.Group("audio.convert", "Convert",
                MenuEntry.Leaf("audio.convert.MP3", "to MP3", "menu.to"),
                MenuEntry.Leaf("audio.convert.M4A", "to M4A", "menu.to"),
                MenuEntry.Leaf("audio.convert.WAV", "to WAV", "menu.to"),
                MenuEntry.Leaf("audio.convert.AIF", "to AIF", "menu.to"),
                MenuEntry.Leaf("audio.convert.AIFF", "to AIFF", "menu.to"),
                MenuEntry.Leaf("audio.convert.AAC", "to AAC", "menu.to"),
                MenuEntry.Leaf("audio.convert.OGG", "to OGG", "menu.to"),
                MenuEntry.Leaf("audio.convert.WMA", "to WMA", "menu.to")),
            MenuEntry.Leaf("audio.subtitles", "Subtitles")),

        MenuEntry.Group("text", "Text",
            MenuEntry.Group("text.convert", "Convert",
                MenuEntry.Leaf("text.convert.TXT", "to TXT", "menu.to"),
                MenuEntry.Leaf("text.convert.JSON", "to JSON", "menu.to"),
                MenuEntry.Leaf("text.convert.XML", "to XML", "menu.to"),
                MenuEntry.Leaf("text.convert.CSV", "to CSV", "menu.to"),
                MenuEntry.Leaf("text.convert.HTML", "to HTML", "menu.to"),
                MenuEntry.Leaf("text.convert.MD", "to MD", "menu.to")),
            MenuEntry.Group("text.encode", "Format",
                MenuEntry.Leaf("text.encode.UTF8", "to UTF-8", "menu.to"),
                MenuEntry.Leaf("text.encode.UTF8BOM", "to UTF-8 (BOM)", "menu.to"),
                MenuEntry.Leaf("text.encode.UTF16", "to UTF-16", "menu.to"),
                MenuEntry.Leaf("text.encode.SJIS", "to Shift_JIS", "menu.to")),
            MenuEntry.Group("text.lineEndings", "Line Endings",
                MenuEntry.Leaf("text.lineEndings.CRLF", "to CRLF (Windows)", "menu.to"),
                MenuEntry.Leaf("text.lineEndings.LF", "to LF (Unix)", "menu.to")),
            MenuEntry.Leaf("json.pretty", "Pretty Print"),
            MenuEntry.Leaf("json.sort", "Sort Keys"),
            MenuEntry.Leaf("python.obfuscate", "Obfuscate Python")),

        MenuEntry.Group("jar", "JAR",
            MenuEntry.Leaf("jar.decompile", "Decompile")),
    ];
}
