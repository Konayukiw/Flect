namespace Flect.Dispatch;

internal static class Dispatch
{
    public static TaskBase? Create(Request request) => request.Id switch
    {
        "folder.removeDuplicate" => new Flect.Folder.Duplicate.Duplicate(request),
        "folder.removeEmpty" => new Flect.Folder.Empty.Empty(request),
        "folder.tree" => new Flect.Folder.Tree.Tree(request),
        "folder.rename" => new Flect.Folder.Rename.Rename(request),
        "folder.analyze" => new Flect.Folder.Analysis.Analyze(request),
        "file.analyze" => new FileAnalyze(request),
        "folder.compress" => new Flect.Folder.Compress.Compress(request),

        "archive.extract" => new Flect.Archive.Extract(request),

        "image.resize" => new Flect.Image.Resize(request),
        "image.compress" => new Flect.Image.Compress(request),
        "image.rotate" => new Flect.Image.Rotate(request),
        "image.convert" => new Flect.Image.Convert(request),
        "image.ocr" => new Flect.Ocr.Ocr(request),
        "image.strip" => new Flect.Image.Metadata(request),
        "image.chromakey" => new Flect.Image.Cutout(request),

        "video.resize" => new Flect.Video.Resize(request),
        "video.trim" => new Flect.Video.Trim.Trim(request),
        "video.thumbnail" => new Flect.Video.Thumbnail.Thumbnail(request),
        "video.subtitles" => new Flect.Subtitles.Subtitles(request),
        "video.compress" => new Flect.Video.Compress.Compress(request),
        "video.rotate" => new Flect.Video.Rotate(request),
        "video.convert" => new Flect.Video.Convert(request),
        "video.extractAudio" => new Flect.Audio.Extract(request),

        "audio.trim" => new Flect.Audio.Trim(request),
        "audio.convert" => new Flect.Audio.Convert(request),
        "audio.subtitles" => new Flect.Subtitles.Subtitles(request),

        "pdf.preview" => new Flect.Pdf.Preview(request),
        "pdf.convert" => new Flect.Pdf.Convert(request),
        "pdf.merge" => new Flect.Pdf.Merge(request),

        "text.encode" => new Flect.Text.Encode(request),
        "text.lineEndings" => new Flect.Text.LineEndings(request),
        "text.convert" => new Flect.Text.Convert(request),
        "json.pretty" => new Flect.Text.Json.JsonText(request, sortKeys: false),
        "json.sort" => new Flect.Text.Json.JsonText(request, sortKeys: true),

        "jar.decompile" => new Flect.Decompile.Decompile(request),

        _ => null,
    };
}
