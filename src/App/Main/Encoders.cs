using System.Diagnostics;
using System.Globalization;

namespace Optimizer.Main;

internal sealed record EncoderChoice(string Name, HardwareEncoder Hardware)
{
    public bool IsHardware => Hardware != HardwareEncoder.None;
}

internal static class Encoders
{
    private static readonly Lazy<HashSet<string>> Installed = new(Probe);

    private static readonly (HardwareEncoder Hardware, string Probe)[] HardwareProbes =
    [
        (HardwareEncoder.Nvenc, "h264_nvenc"),
        (HardwareEncoder.Qsv, "h264_qsv"),
        (HardwareEncoder.Amf, "h264_amf"),
    ];

    public static bool Has(string name) => Installed.Value.Contains(name);

    public static IReadOnlyList<HardwareEncoder> AvailableHardware() =>
        HardwareProbes.Where(probe => Has(probe.Probe))
                      .Select(probe => probe.Hardware)
                      .ToList();

    public static EncoderChoice ForContainer(string extension, VideoSettings settings) =>
        extension switch
        {
            ".webm" => Software(VideoCodecChoice.Vp9),
            ".avi" => new EncoderChoice("mpeg4", HardwareEncoder.None),
            _ => Preferred(settings.Codec, settings.Hardware),
        };

    public static EncoderChoice Preferred(VideoCodecChoice codec, HardwareEncoder hardware)
    {
        if (hardware != HardwareEncoder.None)
        {
            var accelerated = HardwareName(codec, hardware);
            if (accelerated is not null && Has(accelerated))
            {
                return new EncoderChoice(accelerated, hardware);
            }
        }
        return Software(codec);
    }

    private static EncoderChoice Software(VideoCodecChoice codec)
    {
        var name = codec switch
        {
            VideoCodecChoice.H265 => "libx265",
            VideoCodecChoice.Vp9 => "libvpx-vp9",
            VideoCodecChoice.Av1 => Has("libsvtav1") ? "libsvtav1" : "libaom-av1",
            _ => "libx264",
        };

        return new EncoderChoice(Has(name) ? name : "libx264", HardwareEncoder.None);
    }

    private static string? HardwareName(VideoCodecChoice codec, HardwareEncoder hardware)
    {
        var family = hardware switch
        {
            HardwareEncoder.Nvenc => "nvenc",
            HardwareEncoder.Qsv => "qsv",
            HardwareEncoder.Amf => "amf",
            _ => null,
        };
        if (family is null) return null;

        var stem = codec switch
        {
            VideoCodecChoice.H265 => "hevc",
            VideoCodecChoice.Av1 => "av1",
            VideoCodecChoice.Vp9 => "vp9",
            _ => "h264",
        };
        return $"{stem}_{family}";
    }

    public static string[] Quality(EncoderChoice encoder, EncodePriority priority)
    {
        string[] common = ["-c:v", encoder.Name];

        return encoder.Hardware switch
        {
            HardwareEncoder.Nvenc =>
                [.. common, "-preset", NvencPreset(priority), "-rc", "vbr",
                 "-cq", Level(priority, 25, 23, 20), "-b:v", "0"],

            HardwareEncoder.Qsv =>
                [.. common, "-preset", SoftwarePreset(priority),
                 "-global_quality", Level(priority, 25, 23, 20)],

            HardwareEncoder.Amf =>
                [.. common, "-quality", AmfQuality(priority), "-rc", "cqp",
                 "-qp_i", Level(priority, 26, 24, 21), "-qp_p", Level(priority, 26, 24, 21)],

            _ => SoftwareQuality(encoder.Name, priority),
        };
    }

    public static string[] Bitrate(EncoderChoice encoder, int kbps, EncodePriority priority)
    {
        var rate = kbps.ToString(CultureInfo.InvariantCulture) + "k";
        string[] common = ["-c:v", encoder.Name, "-b:v", rate];

        switch (encoder.Hardware)
        {
            case HardwareEncoder.Nvenc:
                return [.. common, "-preset", NvencPreset(priority), "-rc", "vbr"];
            case HardwareEncoder.Qsv:
                return [.. common, "-preset", SoftwarePreset(priority)];
            case HardwareEncoder.Amf:
                return [.. common, "-quality", AmfQuality(priority)];
        }

        return encoder.Name switch
        {
            "libsvtav1" or "libaom-av1" => [.. common, "-preset", Av1Preset(priority)],
            "libvpx-vp9" =>
                [.. common, "-deadline", priority == EncodePriority.Speed ? "realtime" : "good"],
            "mpeg4" => common,
            _ => [.. common, "-preset", SoftwarePreset(priority)],
        };
    }

    public static string[] PixelFormat(EncoderChoice encoder) =>
        encoder.IsHardware ? [] : ["-pix_fmt", "yuv420p"];

    private static string[] SoftwareQuality(string name, EncodePriority priority) => name switch
    {
        "libvpx-vp9" =>
            ["-c:v", name, "-crf", Level(priority, 36, 32, 28), "-b:v", "0"],
        "libsvtav1" or "libaom-av1" =>
            ["-c:v", name, "-crf", Level(priority, 38, 34, 30), "-preset", Av1Preset(priority)],
        "mpeg4" =>
            ["-c:v", name, "-qscale:v", Level(priority, 5, 3, 2)],
        _ =>
            ["-c:v", name, "-crf", Level(priority, 24, 20, 18), "-preset", SoftwarePreset(priority)],
    };

    private static string SoftwarePreset(EncodePriority priority) => priority switch
    {
        EncodePriority.Speed => "veryfast",
        EncodePriority.Quality => "slow",
        _ => "medium",
    };

    private static string NvencPreset(EncodePriority priority) => priority switch
    {
        EncodePriority.Speed => "p2",
        EncodePriority.Quality => "p6",
        _ => "p4",
    };

    private static string AmfQuality(EncodePriority priority) => priority switch
    {
        EncodePriority.Speed => "speed",
        EncodePriority.Quality => "quality",
        _ => "balanced",
    };

    private static string Av1Preset(EncodePriority priority) => priority switch
    {
        EncodePriority.Speed => "10",
        EncodePriority.Quality => "6",
        _ => "8",
    };

    private static string Level(EncodePriority priority, int speed, int balanced, int quality) =>
        (priority switch
        {
            EncodePriority.Speed => speed,
            EncodePriority.Quality => quality,
            _ => balanced,
        }).ToString(CultureInfo.InvariantCulture);

    private static HashSet<string> Probe()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var info = new ProcessStartInfo(Tools.Ffmpeg)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add("-hide_banner");
            info.ArgumentList.Add("-encoders");

            using var process = Process.Start(info);
            if (process is null) return names;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(8000)) return names;

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.Length < 8 || trimmed[0] is not ('V' or 'A' or 'S')) continue;

                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) names.Add(parts[1]);
            }
        }
        catch (Exception)
        {
        }
        return names;
    }
}
