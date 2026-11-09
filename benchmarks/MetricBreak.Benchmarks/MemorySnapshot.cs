
namespace MetricBreak.Benchmarks;

internal readonly record struct MemorySnapshot(
    long HeapBytes,
    long TotalAllocatedBytes,
    long WorkingSetBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    public static MemorySnapshot Capture()
    {
        return new MemorySnapshot(
            GC.GetTotalMemory(forceFullCollection: false),
            GC.GetTotalAllocatedBytes(precise: true),
            Environment.WorkingSet,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2));
    }

    public void PrintDifference(
        MemorySnapshot before,
        string label)
    {
        Console.WriteLine(label);

        Console.WriteLine(
            $"Heap change: {ToMb(HeapBytes - before.HeapBytes):N2} MB");

        Console.WriteLine(
            $"Allocated: {ToMb(TotalAllocatedBytes - before.TotalAllocatedBytes):N2} MB");

        Console.WriteLine(
            $"Working set change: {ToMb(WorkingSetBytes - before.WorkingSetBytes):N2} MB");

        Console.WriteLine(
            $"GC Gen 0: {Gen0Collections - before.Gen0Collections}");

        Console.WriteLine(
            $"GC Gen 1: {Gen1Collections - before.Gen1Collections}");

        Console.WriteLine(
            $"GC Gen 2: {Gen2Collections - before.Gen2Collections}");
    }

    private static double ToMb(long bytes) =>
        bytes / 1024d / 1024d;
}
