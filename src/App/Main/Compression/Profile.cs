namespace Optimizer.Main.Compression;

internal static class Profile
{
    private const double BppDirectAdopt = 0.05;
    private const double BppSampleTest = 0.03;

    private const string BalancedPreset = "fast";

    private readonly record struct Candidate(int Width, int Height, double Fps);

    public static int MakeEven(int value) => Math.Max(2, value / 2 * 2);

    public static EncodingProfile Build(MediaInfo source, int videoKbps, int audioKbps)
    {
        var candidates = Candidates(source);

        foreach (var candidate in candidates)
        {
            double bpp = Bitrate.BitsPerPixelFrame(videoKbps, candidate.Width, candidate.Height,
                                                   candidate.Fps);
            if (bpp >= BppSampleTest)
            {
                return Prof(candidate, videoKbps, audioKbps);
            }
        }

        return Prof(candidates[^1], videoKbps, audioKbps);
    }

    private static EncodingProfile Prof(Candidate candidate, int videoKbps, int audioKbps) =>
        new(candidate.Width, candidate.Height, candidate.Fps, videoKbps, audioKbps, BalancedPreset);

    private static List<Candidate> Candidates(MediaInfo source)
    {
        int width = MakeEven(source.Width);
        int height = MakeEven(source.Height);
        double fps = source.FrameRate > 0 ? source.FrameRate : 30;

        double Clamp(double value) => Math.Min(value, fps);

        var raw = new List<Candidate>
        {
            new(width, height, fps),
            new(width, height, Clamp(30)),
            new(width, height, Clamp(24)),
            ScaledToHeight(width, height, 1080, Clamp(24)),
            ScaledToHeight(width, height, 720, Clamp(24)),
            ScaledToHeight(width, height, 480, Clamp(24)),
        };

        var seen = new HashSet<string>();
        var unique = new List<Candidate>();
        foreach (var candidate in raw)
        {
            if (candidate.Width > width || candidate.Height > height) continue;
            if (candidate.Fps > fps + 0.001) continue;

            var key = $"{candidate.Width}x{candidate.Height}@{candidate.Fps:F3}";
            if (seen.Add(key)) unique.Add(candidate);
        }
        return unique;
    }

    private static Candidate ScaledToHeight(int sourceWidth, int sourceHeight, int targetHeight,
                                            double fps)
    {
        int scaledHeight = MakeEven(Math.Min(targetHeight, sourceHeight));
        int scaledWidth = MakeEven((int)Math.Round(scaledHeight * (double)sourceWidth / sourceHeight));
        return new Candidate(scaledWidth, scaledHeight, fps);
    }
}
