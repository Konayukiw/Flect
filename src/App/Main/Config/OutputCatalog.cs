namespace Optimizer.Main.Config;

internal sealed record OutputFeature(string Key, string CategoryKey, string LabelKey,
                                     IReadOnlyList<string> TaskIds);

internal static class OutputCatalog
{
    public static IReadOnlyList<OutputFeature> Features { get; } =
    [
        new("video.resize", "settings.nav.video", "menu.video.resize", ["video.resize"]),
        new("video.compress", "settings.nav.video", "menu.video.compress", ["video.compress"]),
        new("video.convert", "settings.nav.video", "menu.video.convert", ["video.convert"]),
        new("video.rotate", "settings.nav.video", "menu.video.rotate", ["video.rotate"]),
        new("video.trim", "settings.nav.video", "menu.video.trim", ["video.trim"]),
        new("video.thumbnail", "settings.nav.video", "menu.video.thumbnail", ["video.thumbnail"]),
        new("video.extractAudio", "settings.nav.video", "menu.video.extractAudio", ["video.extractAudio"]),
        new("subtitles", "settings.nav.video", "menu.video.subtitles", ["video.subtitles", "audio.subtitles"]),

        new("image.resize", "settings.nav.image", "menu.image.resize", ["image.resize"]),
        new("image.compress", "settings.nav.image", "menu.image.compress", ["image.compress"]),
        new("image.convert", "settings.nav.image", "menu.image.convert", ["image.convert"]),
        new("image.rotate", "settings.nav.image", "menu.image.rotate", ["image.rotate"]),
        new("image.ocr", "settings.nav.image", "menu.image.ocr", ["image.ocr"]),
        new("image.strip", "settings.nav.image", "menu.image.strip", ["image.strip"]),
        new("image.chromakey", "settings.nav.image", "menu.image.chromakey", ["image.chromakey"]),

        new("audio.trim", "settings.output.audio", "menu.audio.trim", ["audio.trim"]),
        new("audio.convert", "settings.output.audio", "menu.audio.convert", ["audio.convert"]),

        new("text.convert", "settings.nav.text", "menu.text.convert", ["text.convert"]),
        new("text.encode", "settings.nav.text", "menu.text.encode", ["text.encode"]),
        new("text.lineEndings", "settings.nav.text", "menu.text.lineEndings", ["text.lineEndings"]),
        new("json.pretty", "settings.nav.text", "menu.json.pretty", ["json.pretty"]),
        new("json.sort", "settings.nav.text", "menu.json.sort", ["json.sort"]),

        new("pdf.merge", "settings.output.pdf", "menu.pdf.merge", ["pdf.merge"]),
        new("pdf.convert", "settings.output.pdf", "menu.pdf.convert", ["pdf.convert"]),

        new("archive.extract", "settings.output.archive", "menu.archive.extract", ["archive.extract"]),
        new("folder.compress", "settings.nav.folder", "menu.folder.compress", ["folder.compress"]),
    ];

    public static string? KeyFor(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return null;

        foreach (var feature in Features)
        {
            foreach (var id in feature.TaskIds)
            {
                if (string.Equals(id, taskId, StringComparison.OrdinalIgnoreCase)) return feature.Key;
            }
        }
        return null;
    }
}
