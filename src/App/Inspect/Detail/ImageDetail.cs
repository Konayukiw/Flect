using System.Globalization;
using System.Xml;
using ImageMagick;

namespace Flect.Inspect.Detail;

internal static class ImageDetail
{
    private const int TagLimit = 250;

    public static ProbeResult? Read(string path, string kind)
    {
        if (kind == "SVG" || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var svg = Svg(path);
            if (svg is not null) return svg;
        }

        try
        {
            using var frames = new MagickImageCollection();
            frames.Ping(path);
            if (frames.Count == 0) return null;
            return FromFrames(frames);
        }
        catch (MagickException)
        {
            return null;
        }
    }

    private static ProbeResult FromFrames(MagickImageCollection frames)
    {
        var first = frames[0];
        var kind = KindOf(first.Format);
        var image = new FactBag();
        image.Add("dialog.fileAnalyze.format", FormatLabel(first, kind));
        image.Add("dialog.fileAnalyze.dimensions", $"{first.Width} x {first.Height}");
        image.Add("dialog.fileAnalyze.bitDepth", first.Depth > 0 ? first.Depth.ToString(CultureInfo.InvariantCulture) : null);
        image.Add("dialog.fileAnalyze.alpha", Kinds.YesNo(first.HasAlpha));
        image.Add("dialog.fileAnalyze.color", $"{first.ColorType}, {first.ColorSpace}");
        image.Add("dialog.fileAnalyze.dpi", Density(first));
        image.Add("dialog.fileAnalyze.interlace", Interlace(first.Interlace));
        image.Add("dialog.fileAnalyze.profile", Profile(first));
        image.Add("dialog.fileAnalyze.orientation", Orientation(first.Orientation));
        image.Add("dialog.fileAnalyze.comment", Short(first.Comment));

        if (frames.Count > 1 || first.AnimationDelay > 0)
        {
            var delays = frames.Select(frame => (long)frame.AnimationDelay).ToList();
            var total = delays.Sum();
            image.Add("dialog.fileAnalyze.frames", frames.Count.ToString(CultureInfo.InvariantCulture));
            image.Add("dialog.fileAnalyze.duration", $"{total * 10} ms");
            image.Add("dialog.fileAnalyze.loop", Loop(first.AnimationIterations, frames.Count));
            if (delays.Count > 1)
            {
                image.Add("dialog.fileAnalyze.delay",
                          $"{delays.Min() * 10} - {delays.Max() * 10} ms");
            }
        }

        if (kind == "ICO")
        {
            image.Add("dialog.fileAnalyze.sizes",
                      string.Join(", ", frames.Select(frame => $"{frame.Width} x {frame.Height}").Distinct()));
        }

        var sections = new List<FileSection>();
        var main = image.Section("dialog.fileAnalyze.image");
        if (main is not null) sections.Add(main);

        var exif = first.GetExifProfile();
        var camera = Camera(exif);
        if (camera is not null) sections.Add(camera);
        var location = Location(exif);
        if (location is not null) sections.Add(location);
        var tags = Tags(first, exif);
        if (tags is not null) sections.Add(tags);

        return new ProbeResult { Kind = kind, Sections = sections };
    }

    private static FileSection? Camera(IExifProfile? exif)
    {
        if (exif is null) return null;
        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.taken", Named(exif, "DateTimeOriginal", "DateTimeDigitized", "DateTime"));
        bag.Add("dialog.fileAnalyze.make", Named(exif, "Make"));
        bag.Add("dialog.fileAnalyze.model", Named(exif, "Model"));
        bag.Add("dialog.fileAnalyze.exposure", Named(exif, "ExposureTime"));
        bag.Add("dialog.fileAnalyze.fNumber", Named(exif, "FNumber"));
        bag.Add("dialog.fileAnalyze.iso", Named(exif, "PhotographicSensitivity", "ISOSpeedRatings"));
        bag.Add("dialog.fileAnalyze.focal", Named(exif, "FocalLength"));
        bag.Add("dialog.fileAnalyze.flash", Named(exif, "Flash"));
        bag.Add("dialog.fileAnalyze.software", Named(exif, "Software"));
        return bag.Section("dialog.fileAnalyze.camera");
    }

    private static FileSection? Location(IExifProfile? exif)
    {
        if (exif is null) return null;
        var latitude = Gps(Named(exif, "GPSLatitude"), Named(exif, "GPSLatitudeRef"));
        var longitude = Gps(Named(exif, "GPSLongitude"), Named(exif, "GPSLongitudeRef"));
        if (latitude is null && longitude is null) return null;

        var bag = new FactBag();
        bag.Add("dialog.fileAnalyze.latitude", latitude?.ToString("0.######", CultureInfo.InvariantCulture));
        bag.Add("dialog.fileAnalyze.longitude", longitude?.ToString("0.######", CultureInfo.InvariantCulture));
        bag.Add("dialog.fileAnalyze.altitude", Named(exif, "GPSAltitude"));
        return bag.Section("dialog.fileAnalyze.location");
    }

    private static FileSection? Tags(IMagickImage<byte> image, IExifProfile? exif)
    {
        var bag = new FactBag();
        var shown = 0;
        var hidden = 0;

        if (exif is not null)
        {
            foreach (var value in exif.Values)
            {
                if (value.DataType is ExifDataType.Byte or ExifDataType.Undefined) continue;
                var name = value.Tag.ToString();
                if (name.Contains("MakerNote", StringComparison.OrdinalIgnoreCase)) continue;
                var text = Short(value.ToString());
                if (text is null) continue;
                if (shown >= TagLimit)
                {
                    hidden++;
                    continue;
                }
                bag.AddRaw(name, text);
                shown++;
            }
        }

        var iptc = image.GetIptcProfile();
        if (iptc is not null)
        {
            foreach (var value in iptc.Values)
            {
                var text = Short(value.ToString());
                if (text is null) continue;
                if (shown >= TagLimit)
                {
                    hidden++;
                    continue;
                }
                bag.AddRaw(value.Tag.ToString(), text);
                shown++;
            }
        }

        var xmp = image.GetXmpProfile();
        if (xmp is not null)
        {
            try
            {
                var document = xmp.ToXDocument();
                if (document is not null)
                {
                    foreach (var element in document.Descendants())
                    {
                        if (element.HasElements) continue;
                        var text = Short(element.Value);
                        if (text is null) continue;
                        if (shown >= TagLimit)
                        {
                            hidden++;
                            continue;
                        }
                        bag.AddRaw(element.Name.LocalName, text);
                        shown++;
                    }
                }
            }
            catch (XmlException)
            {
            }
        }

        if (hidden > 0) bag.Add("dialog.fileAnalyze.moreTags", hidden.ToString(CultureInfo.InvariantCulture));
        return bag.Section("dialog.fileAnalyze.tags");
    }

    private static ProbeResult? Svg(string path)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = true,
            };
            using var reader = XmlReader.Create(path, settings);
            string? viewBox = null;
            string? width = null;
            string? height = null;
            var images = 0;
            var scripts = 0;
            var external = 0;
            var sawSvg = false;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                {
                    sawSvg = true;
                    viewBox ??= reader.GetAttribute("viewBox");
                    width ??= reader.GetAttribute("width");
                    height ??= reader.GetAttribute("height");
                }
                if (reader.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)) scripts++;
                if (reader.LocalName.Equals("image", StringComparison.OrdinalIgnoreCase)) images++;
                if (External(reader.GetAttribute("href")) || External(reader.GetAttribute("src"))) external++;
            }

            if (!sawSvg) return null;
            var bag = new FactBag();
            bag.Add("dialog.fileAnalyze.format", "SVG");
            bag.Add("dialog.fileAnalyze.viewBox", viewBox);
            if (width is not null || height is not null)
            {
                bag.Add("dialog.fileAnalyze.svgSize", $"{width ?? "?"} x {height ?? "?"}");
            }
            bag.Add("dialog.fileAnalyze.embeddedImages", images.ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.scripts", scripts.ToString(CultureInfo.InvariantCulture));
            bag.Add("dialog.fileAnalyze.externalRefs", external.ToString(CultureInfo.InvariantCulture));
            var section = bag.Section("dialog.fileAnalyze.image");
            return section is null
                ? null
                : new ProbeResult { Kind = "SVG", Sections = [section] };
        }
        catch (Exception ex) when (ex is XmlException or IOException)
        {
            return null;
        }
    }

    private static bool External(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var trimmed = reference.Trim();
        return !trimmed.StartsWith('#') && !trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static string KindOf(MagickFormat format) => format switch
    {
        MagickFormat.Png or MagickFormat.Png00 or MagickFormat.Png8 or MagickFormat.Png24 or
            MagickFormat.Png32 or MagickFormat.Png48 or MagickFormat.Png64 => "PNG",
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe => "JPEG",
        MagickFormat.WebP => "WEBP",
        MagickFormat.Gif or MagickFormat.Gif87 => "GIF",
        MagickFormat.Heic or MagickFormat.Heif => "HEIC",
        MagickFormat.Ico => "ICO",
        MagickFormat.Svg => "SVG",
        MagickFormat.Avif => "AVIF",
        _ => format.ToString().ToUpperInvariant(),
    };

    private static string FormatLabel(IMagickImage<byte> image, string kind)
    {
        if (kind != "WEBP") return kind;
        var lossless = image.GetAttribute("webp:lossless");
        if (string.Equals(lossless, "true", StringComparison.OrdinalIgnoreCase)) return "WebP lossless";
        if (string.Equals(lossless, "false", StringComparison.OrdinalIgnoreCase)) return "WebP lossy";
        return "WebP";
    }

    private static string? Density(IMagickImage<byte> image)
    {
        if (image.Density.X <= 0 || image.Density.Y <= 0) return null;
        var unit = image.Density.Units switch
        {
            DensityUnit.PixelsPerCentimeter => "px/cm",
            _ => "ppi",
        };
        return string.Create(CultureInfo.InvariantCulture,
                              $"{image.Density.X:0.##} x {image.Density.Y:0.##} {unit}");
    }

    private static string? Interlace(Interlace value) => value switch
    {
        ImageMagick.Interlace.NoInterlace => Loc.T("dialog.fileAnalyze.noInterlace"),
        ImageMagick.Interlace.Png => Loc.T("dialog.fileAnalyze.adam7"),
        ImageMagick.Interlace.Jpeg => Loc.T("dialog.fileAnalyze.progressive"),
        ImageMagick.Interlace.Undefined => null,
        _ => value.ToString(),
    };

    private static string? Profile(IMagickImage<byte> image)
    {
        var profile = image.GetColorProfile();
        if (profile is null) return null;
        if (!string.IsNullOrWhiteSpace(profile.Description)) return profile.Description;
        return profile.ColorSpace.ToString();
    }

    private static string? Orientation(OrientationType orientation) => orientation switch
    {
        OrientationType.Undefined => null,
        OrientationType.TopLeft => Loc.T("dialog.fileAnalyze.normal"),
        OrientationType.RightTop => "90 degrees clockwise",
        OrientationType.BottomRight => "180 degrees",
        OrientationType.LeftBottom => "90 degrees counter-clockwise",
        _ => orientation.ToString(),
    };

    private static string? Loop(long iterations, int frames)
    {
        if (frames <= 1) return null;
        return iterations <= 0
            ? Loc.T("dialog.fileAnalyze.infinite")
            : iterations.ToString(CultureInfo.InvariantCulture);
    }

    private static string? Named(IExifProfile exif, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var value in exif.Values)
            {
                if (!string.Equals(value.Tag.ToString(), name, StringComparison.Ordinal)) continue;
                var text = value.ToString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                return text.Trim().Trim('\0');
            }
        }
        return null;
    }

    private static double? Gps(string? text, string? reference)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var values = new double[Math.Min(parts.Length, 3)];
        for (var index = 0; index < values.Length; index++)
        {
            var piece = parts[index];
            var slash = piece.IndexOf('/');
            if (slash > 0 &&
                double.TryParse(piece[..slash], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
                double.TryParse(piece[(slash + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0)
            {
                values[index] = numerator / denominator;
            }
            else if (!double.TryParse(piece, NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]))
            {
                return null;
            }
        }

        var degrees = values[0];
        if (values.Length > 1) degrees += values[1] / 60d;
        if (values.Length > 2) degrees += values[2] / 3600d;
        if (reference is "S" or "s" or "W" or "w") degrees = -degrees;
        return degrees;
    }

    private static string? Short(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().Trim('\0');
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
