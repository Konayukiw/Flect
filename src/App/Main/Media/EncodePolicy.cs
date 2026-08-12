using System.Diagnostics;
using System.Numerics;

namespace Optimizer.Main.Media;

internal static class EncodePolicy
{
    private const int Floor = 1;

    private const int ProbeEvery = 10;

    public static int UsableProcessors()
    {
        int usable = Environment.ProcessorCount;
        try
        {
            ulong mask = (ulong)(long)System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity;
            if (mask != 0) usable = Math.Min(usable, BitOperations.PopCount(mask));
        }
        catch (Exception)
        {
        }
        return Math.Max(1, usable);
    }

    public static int WorkloadCeiling(int outputHeight) => outputHeight switch
    {
        <= 0 => 8,
        <= 720 => 6,
        <= 1080 => 8,
        <= 1440 => 12,
        _ => 16,
    };
    public static IReadOnlyList<int> Ladder(int outputHeight)
    {
        int ceiling = Math.Min(WorkloadCeiling(outputHeight), UsableProcessors());
        int start = Math.Clamp(StartingPoint(ceiling), Floor, ceiling);

        var rungs = new List<int>();
        for (int threads = start; threads > Floor; threads /= 2) rungs.Add(threads);
        rungs.Add(Floor);
        return rungs;
    }

    public static void RecordSuccess(int threads) => Save(new Memory(UsableProcessors(), threads,
                                                                    Load().Runs + 1));

    private static int StartingPoint(int ceiling)
    {
        var memory = Load();

        if (memory.Threads <= 0 || memory.Processors != UsableProcessors()) return ceiling;

        bool probing = memory.Runs > 0 && memory.Runs % ProbeEvery == 0;
        return probing ? Math.Min(ceiling, memory.Threads * 2) : memory.Threads;
    }

    private sealed record Memory(int Processors, int Threads, int Runs);

    private const string FileName = "encoder.json";

    private static Memory Load() => Store.Read<Memory>(FileName) ?? new Memory(0, 0, 0);

    private static void Save(Memory memory) => Store.Write(FileName, memory);
}
