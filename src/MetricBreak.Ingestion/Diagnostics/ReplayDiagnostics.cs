
namespace MetricBreak.Ingestion.Diagnostics;

public sealed record ReplayDiagnostics(
    TimeSpan Elapsed,
    long SamplesRead)
{
    public double SamplesPerSecond =>
        Elapsed.TotalSeconds > 0
            ? SamplesRead / Elapsed.TotalSeconds
            : 0;
}

