namespace Flect.Inspect;

internal static class Examine
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "heic", "heif", "ico", "gif", "svg", "avif", "bmp", "tif", "tiff",
    };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mov", "mkv", "m4a", "avi", "webm", "flv", "wmv",
        "mp3", "wav", "aif", "aiff", "aac", "ogg", "wma", "flac",
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "zip", "7z", "tar", "rar", "gz", "tgz", "jar", "tar.gz",
    };

    public static async Task<FileReport> InspectAsync(string path, CancellationToken token)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Attributes.HasFlag(FileAttributes.Directory))
        {
            throw new InvalidOperationException(Loc.T("dialog.fileAnalyze.single"));
        }

        var name = FileName.Read(info.Name);
        var header = Header(path);
        var magic = Signature.Of(header, info.Name);
        var detected = magic.Kind;
        var executable = magic.Family == "pe";
        var sections = new List<FileSection>();

        if (magic.Family == "image" ||
            (ImageExtensions.Contains(name.Extension) && magic.Family is "binary" or "text" or ""))
        {
            var image = Try(() => ImageDetail.Read(path, magic.Kind == "SVG" ? "SVG" : detected));
            if (image is not null)
            {
                if (!string.IsNullOrEmpty(image.Kind) && magic.Family is "image" or "binary" or "text" or "")
                {
                    detected = image.Kind;
                }
                sections.AddRange(image.Sections);
            }
        }

        if (magic.Family == "pdf" ||
            (name.Extension.Equals("pdf", StringComparison.OrdinalIgnoreCase) && sections.Count == 0))
        {
            var pdf = Try(() => PdfDetail.Read(path));
            if (pdf is not null)
            {
                detected = pdf.Kind ?? detected;
                sections.AddRange(pdf.Sections);
            }
        }

        if (magic.Family == "archive" || ArchiveExtensions.Contains(name.Extension) ||
            name.Extension.Equals("jar", StringComparison.OrdinalIgnoreCase))
        {
            var archive = await TryAsync(() => ArchiveDetail.ReadAsync(path, detected, token));
            if (archive is not null)
            {
                if (!string.IsNullOrEmpty(archive.Kind)) detected = archive.Kind;
                sections.AddRange(archive.Sections);
            }
        }

        if (magic.Family is "video" or "audio" ||
            (MediaExtensions.Contains(name.Extension) && magic.Family is not "image" and not "pdf" and not "archive" and not "pe"))
        {
            var media = await TryAsync(() => MediaDetail.ReadAsync(path, token));
            if (media is not null)
            {
                if (!string.IsNullOrEmpty(media.Kind) &&
                    media.Kind is not "VIDEO" and not "AUDIO" && magic.Family is "video" or "audio" or "binary" or "text" or "")
                {
                    detected = PreferMedia(detected, media.Kind, magic.Family);
                }
                sections.AddRange(media.Sections);
            }
        }

        if (magic.Family == "pe" || name.Extension.Equals("exe", StringComparison.OrdinalIgnoreCase) ||
            name.Extension.Equals("dll", StringComparison.OrdinalIgnoreCase))
        {
            var pe = Try(() => BinaryDetail.Pe(path));
            if (pe is not null)
            {
                detected = "PE";
                executable = true;
                sections.AddRange(pe.Sections);
            }
        }

        if (magic.Family == "lnk" || name.Extension.Equals("lnk", StringComparison.OrdinalIgnoreCase))
        {
            var link = Try(() => BinaryDetail.Link(path));
            if (link is not null)
            {
                detected = "LNK";
                sections.AddRange(link.Sections);
            }
        }

        if (magic.Family == "text" || Kinds.IsTextExtension(name.Extension))
        {
            if (magic.Family is not "image" and not "pdf" and not "archive" and not "pe" and not "video" and not "audio")
            {
                var text = Try(() => TextDetail.Read(path, name.Extension, token));
                if (text is not null)
                {
                    if (!string.IsNullOrEmpty(text.Kind) && text.Kind != "TEXT") detected = text.Kind;
                    else if (detected.Length == 0) detected = "TEXT";
                    sections.AddRange(text.Sections);
                }
            }
        }

        var alerts = new List<string>();
        var claimed = name.ClaimedKind;
        if (!Kinds.Same(claimed, detected))
        {
            var shownDetected = detected.Length == 0 ? Loc.T("dialog.fileAnalyze.unknown") : detected;
            alerts.Add(Loc.F("dialog.fileAnalyze.mismatch", claimed, shownDetected));
        }

        var disguise = name.DisguiseAs;
        if (disguise is null && executable && !Kinds.IsExecutable(name.Extension))
        {
            disguise = claimed.Length == 0 ? Loc.T("dialog.fileAnalyze.unknown") : claimed;
        }
        if (disguise is not null) alerts.Add(Loc.F("dialog.fileAnalyze.disguise", disguise));
        alerts.AddRange(name.Alerts);

        var overview = new FactBag();
        FileSystemDetail.Add(overview, info);
        if (name.MarkedName is not null) overview.Add("dialog.fileAnalyze.actualName", name.MarkedName);
        if (name.ShownName is not null) overview.Add("dialog.fileAnalyze.shown", name.ShownName);
        overview.Add("dialog.fileAnalyze.extension", claimed.Length == 0 ? Loc.T("dialog.fileAnalyze.none") : claimed);
        overview.Add("dialog.fileAnalyze.contents", detected.Length == 0 ? Loc.T("dialog.fileAnalyze.unknown") : detected);

        return new FileReport
        {
            Path = info.FullName,
            Alerts = alerts,
            Overview = overview.Rows.ToList(),
            Sections = sections,
        };
    }

    private static string PreferMedia(string detected, string media, string family)
    {
        if (family is "video" or "audio" && detected is not "" and not "TEXT")
        {
            if (detected is "MP4" && media is "M4A") return media;
            if (detected is "WMA" && media is "WMV") return media;
            if (detected is "MKV" && media is "WEBM") return media;
            return detected;
        }
        return media;
    }

    private static byte[] Header(string path)
    {
        using var stream = File.OpenRead(path);
        var length = (int)Math.Min(stream.Length, 8192);
        var buffer = new byte[length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static ProbeResult? Try(Func<ProbeResult?> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(ex);
        }
    }

    private static async Task<ProbeResult?> TryAsync(Func<Task<ProbeResult?>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(ex);
        }
    }

    private static ProbeResult Error(Exception ex)
    {
        var bag = new FactBag();
        var message = ex.Message.Trim();
        if (message.Length > 300) message = message[..300];
        bag.Add("dialog.fileAnalyze.unreadable", message);
        var section = bag.Section("dialog.fileAnalyze.unreadableTitle");
        return new ProbeResult { Sections = section is null ? [] : [section] };
    }
}
