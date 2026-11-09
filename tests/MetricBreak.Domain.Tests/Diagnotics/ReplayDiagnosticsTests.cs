
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Diagnostics;

namespace MetricBreak.Domain.Tests.Diagnostics;

public sealed class ReplayDiagnosticsTests
{
    [Fact]
    public void Read_SuccessfulRead_IncrementsCounter()
    {
        var inner = CreateReader();
        var reader = new InstrumentedTelemetryReader(inner);

        reader.Read(0);
        reader.Read(1);

        Assert.Equal(2, reader.ReadCount);
    }

    [Fact]
    public void Read_InvalidSequence_DoesNotIncrementCounter()
    {
        var reader = new InstrumentedTelemetryReader(CreateReader());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reader.Read(99));

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public void ResetCounters_ResetsReadCount()
    {
        var reader = new InstrumentedTelemetryReader(CreateReader());

        reader.Read(0);
        reader.ResetCounters();

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public void Measure_CountsReadsDuringOperation()
    {
        var reader = new InstrumentedTelemetryReader(CreateReader());
        var runner = new ReplayDiagnosticsRunner();

        var diagnostics = runner.Measure(reader, () =>
        {
            reader.Read(0);
            reader.Read(1);
        });

        Assert.Equal(2, diagnostics.SamplesRead);
        Assert.True(diagnostics.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Measure_ResetsPreviousReadCount()
    {
        var reader = new InstrumentedTelemetryReader(CreateReader());

        reader.Read(0);

        var diagnostics = new ReplayDiagnosticsRunner()
            .Measure(reader, () => reader.Read(1));

        Assert.Equal(1, diagnostics.SamplesRead);
    }

    [Fact]
    public void SamplesPerSecond_UsesElapsedTime()
    {
        var diagnostics = new ReplayDiagnostics(
            TimeSpan.FromSeconds(2),
            100);

        Assert.Equal(50, diagnostics.SamplesPerSecond);
    }

    [Fact]
    public void SamplesPerSecond_ZeroElapsed_ReturnsZero()
    {
        var diagnostics = new ReplayDiagnostics(
            TimeSpan.Zero,
            100);

        Assert.Equal(0, diagnostics.SamplesPerSecond);
    }

    [Fact]
    public void Constructor_NullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InstrumentedTelemetryReader(null!));
    }

    [Fact]
    public void Measure_NullOperation_Throws()
    {
        var reader = new InstrumentedTelemetryReader(CreateReader());

        Assert.Throws<ArgumentNullException>(() =>
            new ReplayDiagnosticsRunner().Measure(reader, null!));
    }

    private static ITelemetryReader CreateReader()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,100
            2026-11-08T10:00:01Z,200
            """;

        using var textReader = new StringReader(csv);

        var result = new MetricBreak.Ingestion.Csv.CsvTelemetryParser()
            .Parse(textReader);

        return new MetricBreak.Ingestion.Csv.CsvTelemetryReader(
            Guid.NewGuid(),
            result);
    }
}
