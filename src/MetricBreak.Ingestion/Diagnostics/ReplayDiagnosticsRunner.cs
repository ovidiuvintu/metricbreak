
using System.Diagnostics;

namespace MetricBreak.Ingestion.Diagnostics;

public sealed class ReplayDiagnosticsRunner
{
    public ReplayDiagnostics Measure(
        InstrumentedTelemetryReader reader,
        Action replayOperation)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(replayOperation);

        reader.ResetCounters();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            replayOperation();
        }
        finally
        {
            stopwatch.Stop();
        }

        return new ReplayDiagnostics(
            stopwatch.Elapsed,
            reader.ReadCount);
    }
}
