using System.Diagnostics;
using Microsoft.Win32;

namespace Optimizer.Main.Media;

internal static class Accelaration
{
    private const string FileName = "hardware.json";

    private const int ProbeTimeoutMs = 15000;

    private sealed record Probe(string Fingerprint, List<string> Working);

    private static readonly (HardwareEncoder Hardware, string Encoder)[] Candidates =
    [
        (HardwareEncoder.Nvenc, "h264_nvenc"),
        (HardwareEncoder.Qsv, "h264_qsv"),
        (HardwareEncoder.Amf, "h264_amf"),
    ];

    public static IReadOnlyList<HardwareEncoder> Usable()
    {
        var fingerprint = Fingerprint();

        var cached = Store.Read<Probe>(FileName);
        if (cached is not null && cached.Fingerprint == fingerprint) return Decode(cached.Working);

        var working = new List<string>();
        foreach (var (hardware, encoder) in Candidates)
        {
            if (Encoders.Has(encoder) && CanEncode(encoder)) working.Add(hardware.ToString());
        }

        Store.Write(FileName, new Probe(fingerprint, working));
        return Decode(working);
    }

    public static bool KnownUnusable(HardwareEncoder hardware)
    {
        if (hardware == HardwareEncoder.None) return false;

        var cached = Store.Read<Probe>(FileName);
        if (cached is null || cached.Fingerprint != Fingerprint()) return false;

        return !Contains(cached.Working, hardware);
    }

    private static bool CanEncode(string encoder)
    {
        try
        {
            var info = new ProcessStartInfo(Tools.Ffmpeg)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in new[]
                     {
                         "-hide_banner", "-loglevel", "error", "-nostdin",
                         "-f", "lavfi", "-i", "color=c=black:s=320x240:d=1",
                         "-c:v", encoder, "-f", "null", "-",
                     })
            {
                info.ArgumentList.Add(argument);
            }

            using var process = System.Diagnostics.Process.Start(info);
            if (process is null) return false;

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(ProbeTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) {}
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IReadOnlyList<HardwareEncoder> Decode(List<string> working)
    {
        var usable = new List<HardwareEncoder>();
        foreach (var (hardware, _) in Candidates)
        {
            if (Contains(working, hardware)) usable.Add(hardware);
        }
        return usable;
    }

    private static bool Contains(List<string> working, HardwareEncoder hardware) =>
        working.Any(name => string.Equals(name, hardware.ToString(),
                                          StringComparison.OrdinalIgnoreCase));

    private static string Fingerprint()
    {
        var parts = new List<string>();
        try
        {
            var ffmpeg = new FileInfo(Tools.Ffmpeg);
            parts.Add($"ffmpeg:{ffmpeg.Length}@{ffmpeg.LastWriteTimeUtc.Ticks}");
        }
        catch (Exception)
        {
            parts.Add("ffmpeg:none");
        }
        parts.AddRange(DisplayAdapters());
        return string.Join('|', parts);
    }

    private static List<string> DisplayAdapters()
    {
        const string ClassKey =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        var adapters = new List<string>();
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(ClassKey);
            if (root is null) return adapters;

            foreach (var name in root.GetSubKeyNames())
            {
                if (name.Length != 4 || !int.TryParse(name, out _)) continue;

                using var adapter = root.OpenSubKey(name);
                if (adapter?.GetValue("DriverDesc") is not string description) continue;

                adapters.Add($"{description}@{adapter.GetValue("DriverVersion") as string ?? "?"}");
            }
        }
        catch (Exception)
        {
        }

        adapters.Sort(StringComparer.Ordinal);
        return adapters;
    }
}
