using System.Globalization;
using System.Text.Json;

namespace Flect.Inspect.Detail;

internal static class MediaDetail
{
    public static async Task<ProbeResult?> ReadAsync(string path, CancellationToken token)
    {
        ProcessResult result;
        try
        {
            result = await Flect.External.ChildProcess.RunOrThrowAsync(Tools.Ffprobe,
            [
                "-v", "error",
                "-print_format", "json",
                "-show_format",
                "-show_streams",
                "-show_chapters",
                path,
            ], token);
        }
        catch (Exception ex) when (ex is FileNotFoundException or ProcessFailure)
        {
            return null;
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        var sections = new List<FileSection>();
        var overview = new FactBag();
        string? container = null;
        if (root.TryGetProperty("format", out var format))
        {
            container = Text(format, "format_long_name") ?? Text(format, "format_name");
            overview.Add("dialog.fileAnalyze.container", container);
            overview.Add("dialog.fileAnalyze.duration", Duration(Text(format, "duration")));
            overview.Add("dialog.fileAnalyze.bitrate", Bitrate(Text(format, "bit_rate")));
            if (format.TryGetProperty("tags", out var tags)) Tags(overview, tags);
        }

        var chapters = root.TryGetProperty("chapters", out var chapterList) &&
                       chapterList.ValueKind == JsonValueKind.Array
            ? chapterList.GetArrayLength()
            : 0;
        if (chapters > 0)
        {
            overview.Add("dialog.fileAnalyze.chapters", chapters.ToString(CultureInfo.InvariantCulture));
        }

        var cover = 0;
        var video = 0;
        var audio = 0;
        var subtitle = 0;
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (Attached(stream))
                {
                    cover++;
                    continue;
                }

                var type = Text(stream, "codec_type");
                FileSection? section = type switch
                {
                    "video" => Video(stream, ++video),
                    "audio" => Audio(stream, ++audio),
                    "subtitle" => Subtitle(stream, ++subtitle),
                    _ => null,
                };
                if (section is not null) sections.Add(section);
            }
        }

        if (cover > 0) overview.Add("dialog.fileAnalyze.cover", cover.ToString(CultureInfo.InvariantCulture));
        var head = overview.Section("dialog.fileAnalyze.media");
        if (head is not null) sections.Insert(0, head);
        if (sections.Count == 0) return null;

        var kind = KindFrom(container, video, audio);
        return new ProbeResult { Kind = kind, Sections = sections };
    }

    private static string? KindFrom(string? container, int video, int audio)
    {
        if (string.IsNullOrEmpty(container)) return video > 0 ? "MP4" : audio > 0 ? "AUDIO" : null;
        var lower = container.ToLowerInvariant();
        if (lower.Contains("webm")) return "WEBM";
        if (lower.Contains("matroska")) return "MKV";
        if (lower.Contains("avi")) return "AVI";
        if (lower.Contains("flv")) return "FLV";
        if (lower.Contains("asf") || lower.Contains("wmv") || lower.Contains("wma"))
        {
            return video > 0 ? "WMV" : "WMA";
        }
        if (lower.Contains("mp3")) return "MP3";
        if (lower.Contains("wav")) return "WAV";
        if (lower.Contains("aiff")) return "AIFF";
        if (lower.Contains("ogg")) return "OGG";
        if (lower.Contains("flac")) return "FLAC";
        if (lower.Contains("mov") && !lower.Contains("mp4")) return video > 0 ? "MOV" : "M4A";
        if (lower.Contains("mp4") || lower.Contains("mov") || lower.Contains("m4a"))
        {
            return video > 0 ? "MP4" : "M4A";
        }
        return video > 0 ? "VIDEO" : audio > 0 ? "AUDIO" : null;
    }

    private static void Tags(FactBag bag, JsonElement tags)
    {
        bag.Add("dialog.fileAnalyze.metaTitle", Text(tags, "title"));
        bag.Add("dialog.fileAnalyze.artist", Text(tags, "artist") ?? Text(tags, "album_artist"));
        bag.Add("dialog.fileAnalyze.album", Text(tags, "album"));
        bag.Add("dialog.fileAnalyze.track", Text(tags, "track"));
        bag.Add("dialog.fileAnalyze.year", Text(tags, "date") ?? Text(tags, "year"));
        bag.Add("dialog.fileAnalyze.genre", Text(tags, "genre"));
        bag.Add("dialog.fileAnalyze.encoder", Text(tags, "encoder") ?? Text(tags, "encoded_by"));
    }

    private static FileSection? Video(JsonElement stream, int index)
    {
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.codec", Codec(stream));
        bag.Add("dialog.fileAnalyze.profileName", Text(stream, "profile"));
        var width = Int(stream, "width");
        var height = Int(stream, "height");
        if (width > 0 && height > 0)
        {
            bag.Add("dialog.fileAnalyze.resolution", $"{width} x {height}");
            var rotation = Rotation(stream);
            var (displayWidth, displayHeight) = Display(width, height, Text(stream, "sample_aspect_ratio"), rotation);
            if (displayWidth != width || displayHeight != height)
            {
                bag.Add("dialog.fileAnalyze.displaySize", $"{displayWidth} x {displayHeight}");
            }
            if (rotation != 0)
            {
                bag.Add("dialog.fileAnalyze.rotation", rotation.ToString(CultureInfo.InvariantCulture) + " degrees");
            }
        }
        bag.Add("dialog.fileAnalyze.fps", Fps(stream));
        bag.Add("dialog.fileAnalyze.pixelFormat", Text(stream, "pix_fmt"));
        bag.Add("dialog.fileAnalyze.bitDepth", Positive(Text(stream, "bits_per_raw_sample")));
        bag.Add("dialog.fileAnalyze.bitrate", Bitrate(Text(stream, "bit_rate")));
        bag.Add("dialog.fileAnalyze.colorSpace", Text(stream, "color_space"));
        bag.Add("dialog.fileAnalyze.transfer", Text(stream, "color_transfer"));
        bag.Add("dialog.fileAnalyze.range", Text(stream, "color_range"));
        bag.Add("dialog.fileAnalyze.language", Language(stream));
        return Named(bag, "dialog.fileAnalyze.video", index);
    }

    private static FileSection? Audio(JsonElement stream, int index)
    {
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.codec", Codec(stream));
        bag.Add("dialog.fileAnalyze.profileName", Text(stream, "profile"));
        bag.Add("dialog.fileAnalyze.sampleRate", SampleRate(Text(stream, "sample_rate")));
        var channels = Int(stream, "channels");
        var layout = Text(stream, "channel_layout");
        if (channels > 0)
        {
            bag.Add("dialog.fileAnalyze.channels",
                    layout is null ? channels.ToString(CultureInfo.InvariantCulture) : $"{channels} ({layout})");
        }
        bag.Add("dialog.fileAnalyze.bitDepth", Positive(Text(stream, "bits_per_sample") ?? Text(stream, "bits_per_raw_sample")));
        bag.Add("dialog.fileAnalyze.bitrate", Bitrate(Text(stream, "bit_rate")));
        bag.Add("dialog.fileAnalyze.language", Language(stream));
        return Named(bag, "dialog.fileAnalyze.audio", index);
    }

    private static FileSection? Subtitle(JsonElement stream, int index)
    {
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.codec", Codec(stream));
        bag.Add("dialog.fileAnalyze.language", Language(stream));
        return Named(bag, "dialog.fileAnalyze.subtitle", index);
    }

    private static FileSection? Named(FactBag bag, string titleKey, int index)
    {
        if (bag.Rows.Count == 0) return null;
        return new FileSection
        {
            Title = Loc.F("dialog.fileAnalyze.stream", Loc.T(titleKey), index),
            Rows = bag.Rows.ToList(),
        };
    }

    private static bool Attached(JsonElement stream)
    {
        if (!stream.TryGetProperty("disposition", out var disposition)) return false;
        return Int(disposition, "attached_pic") == 1;
    }

    private static string? Codec(JsonElement stream)
    {
        var name = Text(stream, "codec_name");
        var longName = Text(stream, "codec_long_name");
        if (name is null) return longName;
        return longName is null ? name : $"{name} ({longName})";
    }

    private static string? Language(JsonElement stream) =>
        stream.TryGetProperty("tags", out var tags) ? Text(tags, "language") : null;

    private static string? Fps(JsonElement stream)
    {
        var rate = ParseRational(Text(stream, "avg_frame_rate"));
        if (rate <= 0) rate = ParseRational(Text(stream, "r_frame_rate"));
        return rate <= 0 ? null : rate.ToString("0.##", CultureInfo.InvariantCulture) + " fps";
    }

    private static int Rotation(JsonElement stream)
    {
        if (stream.TryGetProperty("side_data_list", out var side) && side.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in side.EnumerateArray())
            {
                if (item.TryGetProperty("rotation", out var value) && value.ValueKind == JsonValueKind.Number)
                {
                    return (int)Math.Round(value.GetDouble());
                }
            }
        }

        return stream.TryGetProperty("tags", out var tags) &&
               int.TryParse(Text(tags, "rotate"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacy)
            ? legacy
            : 0;
    }

    private static (int Width, int Height) Display(int width, int height, string? sampleAspect, int rotation)
    {
        var ratio = ParseRational(sampleAspect);
        var displayWidth = ratio > 0 ? (int)Math.Round(width * ratio) : width;
        if (Math.Abs(rotation) % 180 == 90) return (height, displayWidth);
        return (displayWidth, height);
    }

    private static string? Duration(string? text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) return null;
        return Flect.Display.Format.Timecode(seconds);
    }

    private static string? Bitrate(string? text)
    {
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits) || bits <= 0)
        {
            return null;
        }
        var kbps = bits / 1000d;
        return kbps.ToString(kbps >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture) + " kbps";
    }

    private static string? SampleRate(string? text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate) || rate <= 0)
        {
            return null;
        }
        return rate.ToString("N0", CultureInfo.CurrentCulture) + " Hz";
    }

    private static string? Positive(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static double ParseRational(string? text)
    {
        if (string.IsNullOrEmpty(text) || text == "0/0") return 0;
        var parts = text.Split('/');
        if (parts.Length != 2)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain)
                ? plain
                : 0;
        }
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) ||
            denominator == 0)
        {
            return 0;
        }
        return numerator / denominator;
    }
}
