
using System.Diagnostics;

namespace MetricBreak.Benchmarks;

internal static class RepresentationBenchmark
{
    private const int SampleCount = 32_400;
    private const int ChannelCount = 50;

    private static object? _retained;

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("Representation Benchmark");
        Console.WriteLine("------------------------");
        Console.WriteLine($"Samples: {SampleCount:N0}");
        Console.WriteLine($"Channels: {ChannelCount}");
        Console.WriteLine();

        var channelIds = Enumerable.Range(0, ChannelCount)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        // Create source data before measuring either
        // representation.
        var sourceValues = new double[SampleCount][];

        for (var sample = 0; sample < SampleCount; sample++)
        {
            var values = new double[ChannelCount];

            for (var channel = 0; channel < ChannelCount; channel++)
            {
                values[channel] =
                    1000 + channel * 10 + sample % 100;
            }

            sourceValues[sample] = values;
        }

        // Ensure any previously retained benchmark data
        // is released before the first measurement.
        _retained = null;
        ForceFullCollection();

        Measure(
            "Dictionary<Guid, double?>",
            () => BuildDictionarySamples(
                channelIds,
                sourceValues));

        _retained = null;
        ForceFullCollection();

        Measure(
            "double?[]",
            () => BuildArraySamples(sourceValues));

        _retained = null;
        ForceFullCollection();

        GC.KeepAlive(channelIds);
        GC.KeepAlive(sourceValues);
    }

    private static void Measure(
        string name,
        Func<object> build)
    {
        ForceFullCollection();

        var before = MemorySnapshot.Capture();

        var watch = Stopwatch.StartNew();

        var result = build();

        watch.Stop();

        var afterBuild = MemorySnapshot.Capture();

        // Keep the result strongly reachable while
        // measuring its retained heap contribution.
        _retained = result;

        ForceFullCollection();

        var afterCollection = MemorySnapshot.Capture();

        Console.WriteLine(name);
        Console.WriteLine(new string('-', name.Length));

        Console.WriteLine(
            $"Construction: {watch.Elapsed.TotalMilliseconds:N2} ms");

        Console.WriteLine(
            $"Allocated: {ToMb(afterBuild.TotalAllocatedBytes - before.TotalAllocatedBytes):N2} MB");

        Console.WriteLine(
            $"Retained heap: {ToMb(afterCollection.HeapBytes - before.HeapBytes):N2} MB");

        Console.WriteLine(
            $"GC Gen 0: {afterBuild.Gen0Collections - before.Gen0Collections}");

        Console.WriteLine(
            $"GC Gen 1: {afterBuild.Gen1Collections - before.Gen1Collections}");

        Console.WriteLine(
            $"GC Gen 2: {afterBuild.Gen2Collections - before.Gen2Collections}");

        Console.WriteLine();

        GC.KeepAlive(result);
    }

    private static Dictionary<Guid, double?>[] BuildDictionarySamples(
        Guid[] channelIds,
        double[][] sourceValues)
    {
        var samples =
            new Dictionary<Guid, double?>[SampleCount];

        for (var sample = 0; sample < SampleCount; sample++)
        {
            var values = new Dictionary<Guid, double?>(
                ChannelCount);

            for (var channel = 0; channel < ChannelCount; channel++)
            {
                values.Add(
                    channelIds[channel],
                    sourceValues[sample][channel]);
            }

            samples[sample] = values;
        }

        return samples;
    }

    private static double?[][] BuildArraySamples(
        double[][] sourceValues)
    {
        var samples = new double?[SampleCount][];

        for (var sample = 0; sample < SampleCount; sample++)
        {
            var values = new double?[ChannelCount];

            for (var channel = 0; channel < ChannelCount; channel++)
            {
                values[channel] =
                    sourceValues[sample][channel];
            }

            samples[sample] = values;
        }

        return samples;
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static double ToMb(long bytes)
    {
        return bytes / 1024d / 1024d;
    }
}
