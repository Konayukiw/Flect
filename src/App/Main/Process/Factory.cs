namespace Optimizer.Main.Process;

internal static class Factory
{
    public static TaskBase? Create(Request request) => request.Id switch
    {
        "folder.removeDuplicate" => new FolderRemoveDuplicate(request),
        "folder.removeEmpty" => new FolderRemoveEmpty(request),
        "folder.tree" => new FolderTree(request),
        "folder.rename" => new FolderRename(request),
        "folder.analyze" => new FolderAnalyze(request),
        "folder.compress" => new FolderCompress(request),

        "archive.extract" => new ArchiveExtract(request),

        "image.resize" => new ImageResize(request),
        "image.compress" => new ImageCompress(request),
        "image.rotate" => new ImageRotate(request),
        "image.convert" => new ImageConvert(request),
        "image.ocr" => new ImageOcr(request),
        "image.strip" => new ImageStripMetadata(request),
        "image.chromakey" => new ImageChromaKey(request),

        "video.resize" => new VideoResize(request),
        "video.trim" => new VideoTrim(request),
        "video.thumbnail" => new VideoThumbnail(request),
        "video.subtitles" => new MediaSubtitles(request),
        "video.compress" => new VideoCompress(request),
        "video.rotate" => new VideoRotate(request),
        "video.convert" => new VideoConvert(request),
        "video.extractAudio" => new VideoExtractAudio(request),

        "audio.convert" => new AudioConvert(request),
        "audio.subtitles" => new MediaSubtitles(request),

        "pdf.preview" => new PdfPreview(request),
        "pdf.convert" => new PdfConvert(request),
        "pdf.merge" => new PdfMerge(request),

        "text.encode" => new TextEncode(request),
        "text.lineEndings" => new TextLineEndings(request),
        "text.convert" => new TextConvert(request),
        "json.pretty" => new JsonFormat(request, sortKeys: false),
        "json.sort" => new JsonFormat(request, sortKeys: true),

        "jar.decompile" => new JarDecompile(request),
        
        "python.obfuscate" => new PythonObfuscate(request),

        _ => null,
    };
}
