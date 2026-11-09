
using System.Diagnostics;
using System.Globalization;
using System.Text;
using MetricBreak.Benchmarks;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;
using MetricBreak.Ingestion.Diagnostics;

const int sampleCount = 32_400;
const int channelCount = 50;

var filePath = Path.Combine(
    Path.GetTempPath(),
    $"metricbreak-benchmark-{Guid.NewGuid():N}.csv");

try
{
    Console.WriteLine("MetricBreak Replay Benchmark");
    Console.WriteLine("----------------------------");
    Console.WriteLine($"Samples: {sampleCount:N0}");
    Console.WriteLine($"Channels: {channelCount}");
    Console.WriteLine();

    // --------------------------------------------------
    // 1. Generate synthetic telemetry
    // --------------------------------------------------

    GenerateCsv(filePath, sampleCount, channelCount);

    var fileSize = new FileInfo(filePath).Length;

    Console.WriteLine(
        $"CSV size: {ToMb(fileSize):N2} MB");

    // --------------------------------------------------
    // 2. Import telemetry and measure memory
    // --------------------------------------------------

    var importer = new CsvRecordingImporter();

    // Establish a cleaner baseline before importing.
    // This collection is outside the measured duration.
    ForceFullCollection();

    var beforeImport = MemorySnapshot.Capture();

    var importWatch = Stopwatch.StartNew();

    var imported = importer.Import(filePath);

    importWatch.Stop();

    var afterImport = MemorySnapshot.Capture();

    Console.WriteLine();
    Console.WriteLine("Import Performance");
    Console.WriteLine("------------------");

    Console.WriteLine(
        $"Elapsed: {importWatch.Elapsed.TotalMilliseconds:N2} ms");

    afterImport.PrintDifference(
        beforeImport,
        "Memory during import");

    // --------------------------------------------------
    // 3. Estimate retained memory
    // --------------------------------------------------

    // Collect temporary parser allocations while keeping
    // the imported recording available for replay.
    ForceFullCollection();

    var afterCollection = MemorySnapshot.Capture();

    Console.WriteLine();
    Console.WriteLine("After Full Garbage Collection");
    Console.WriteLine("-----------------------------");

    afterCollection.PrintDifference(
        beforeImport,
        "Retained memory estimate");

    // --------------------------------------------------
    // 4. Create replay session
    // --------------------------------------------------

    var reader = new InstrumentedTelemetryReader(
        imported.Reader);

    var session = new ReplaySession(
        imported.Recording,
        reader);

    var pressureChannel = imported.Channels[0];

    var breakpoint = new ConditionalBreakpoint(
        Guid.NewGuid(),
        pressureChannel.Id,
        ComparisonOperator.GreaterThan,
        9000);

    var breakpoints = new BreakpointCollection();
    breakpoints.Add(breakpoint);

    var runner = new ReplayDiagnosticsRunner();

    // --------------------------------------------------
    // 5. Measure breakpoint scan
    // --------------------------------------------------

    BreakpointStop? stop = null;

    var diagnostics = runner.Measure(reader, () =>
    {
        stop = session.RunToBreakpoints(breakpoints);
    });

    Console.WriteLine();
    Console.WriteLine("Breakpoint Scan");
    Console.WriteLine("---------------");

    Console.WriteLine(
        $"Elapsed: {diagnostics.Elapsed.TotalMilliseconds:N2} ms");

    Console.WriteLine(
        $"Samples read: {diagnostics.SamplesRead:N0}");

    Console.WriteLine(
        $"Throughput: {diagnostics.SamplesPerSecond:N0} reads/sec");

    // --------------------------------------------------
    // 6. Validate breakpoint result
    // --------------------------------------------------

    if (stop is null)
    {
        throw new InvalidOperationException(
            "Expected breakpoint was not reached.");
    }

    if (stop.Position.SequenceNumber != sampleCount - 1)
    {
        throw new InvalidOperationException(
            "Breakpoint stopped at the wrong sequence.");
    }

    var hit = stop.Hits.Single();

    Console.WriteLine();
    Console.WriteLine("Breakpoint Hit");
    Console.WriteLine("--------------");

    Console.WriteLine(
        $"Sequence: {stop.Position.SequenceNumber:N0}");

    Console.WriteLine(
        $"Observed value: {hit.ObservedValue:N0}");

    Console.WriteLine(
        $"Threshold: {hit.Threshold:N0}");

    // Keep the imported recording alive through all
    // measurements, including the forced GC snapshot.
    GC.KeepAlive(imported);

    Console.WriteLine();
    Console.WriteLine("Benchmark completed successfully.");

    RepresentationBenchmark.Run();
}
finally
{
    if (File.Exists(filePath))
    {
        File.Delete(filePath);
    }
}

// ======================================================
// Helper methods
// ======================================================

static void ForceFullCollection()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}

static double ToMb(long bytes)
{
    return bytes / 1024d / 1024d;
}

static void GenerateCsv(
    string path,
    int samples,
    int channels)
{
    using var writer = new StreamWriter(
        path,
        false,
        new UTF8Encoding(false));

    // CSV header
    writer.Write("Timestamp");

    for (var channel = 0; channel < channels; channel++)
    {
        writer.Write($",Channel{channel:D2}");
    }

    writer.WriteLine();

    var start = new DateTimeOffset(
        2026, 11, 8, 0, 0, 0, TimeSpan.Zero);

    // Generate one row per second.
    for (var sequence = 0; sequence < samples; sequence++)
    {
        var timestamp = start.AddSeconds(sequence);

        writer.Write(timestamp.ToString(
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture));

        for (var channel = 0; channel < channels; channel++)
        {
            // Normal values remain below the breakpoint.
            // Only the final sample exceeds the threshold.
            double value =
                channel == 0 && sequence == samples - 1
                    ? 9500
                    : 1000 + channel * 10 + sequence % 100;

            writer.Write(',');

            writer.Write(value.ToString(
                CultureInfo.InvariantCulture));
        }

        writer.WriteLine();
    }
}

