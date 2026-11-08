
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class MultipleBreakpointTests
{
    private readonly Guid _recordingId = Guid.NewGuid();
    private readonly Guid _pressureId = Guid.NewGuid();
    private readonly Guid _rateId = Guid.NewGuid();

    private ReplaySession CreateSession(
        params (double Pressure, double Rate)[] measurements)
    {
        var recording = new Recording(
            _recordingId,
            "Multi-Breakpoint Test",
            new RecordingSource("test.csv", "test-hash", 100),
            DateTimeOffset.UtcNow);

        var samples = measurements
            .Select((measurement, index) =>
                new TelemetrySample(
                    index,
                    new TelemetryTimestamp(
                        index * 1_000_000_000L),
                    new Dictionary<Guid, double?>
                    {
                        [_pressureId] = measurement.Pressure,
                        [_rateId] = measurement.Rate
                    }))
            .ToArray();

        var reader = new InMemoryTelemetryReader(
            _recordingId,
            samples);

        return new ReplaySession(recording, reader);
    }

    private ConditionalBreakpoint PressureAbove9000() =>
        new(
            Guid.NewGuid(),
            _pressureId,
            ComparisonOperator.GreaterThan,
            9000);

    private ConditionalBreakpoint RateBelow60() =>
        new(
            Guid.NewGuid(),
            _rateId,
            ComparisonOperator.LessThan,
            60);

    [Fact]
    public void RunToBreakpoints_StopsAtEarliestMatch()
    {
        var session = CreateSession(
            (8500, 72),
            (8800, 70),
            (9100, 68),
            (9300, 55));

        var pressure = PressureAbove9000();
        var rate = RateBelow60();

        var stop = session.RunToBreakpoints(
            new[] { pressure, rate });

        Assert.NotNull(stop);
        Assert.Equal(2, stop.Position.SequenceNumber);
        Assert.Single(stop.Hits);
        Assert.Equal(pressure.Id, stop.Hits[0].BreakpointId);
    }

    [Fact]
    public void RunToBreakpoints_ReturnsAllMatchesAtSameSample()
    {
        var session = CreateSession(
            (8500, 72),
            (9300, 55));

        var pressure = PressureAbove9000();
        var rate = RateBelow60();

        var stop = session.RunToBreakpoints(
            new[] { pressure, rate });

        Assert.NotNull(stop);
        Assert.Equal(1, stop.Position.SequenceNumber);
        Assert.Equal(2, stop.Hits.Count);
        Assert.Equal(pressure.Id, stop.Hits[0].BreakpointId);
        Assert.Equal(rate.Id, stop.Hits[1].BreakpointId);
    }

    [Fact]
    public void RunToBreakpoints_PreservesBreakpointOrder()
    {
        var session = CreateSession(
            (8500, 72),
            (9300, 55));

        var rate = RateBelow60();
        var pressure = PressureAbove9000();

        var stop = session.RunToBreakpoints(
            new[] { rate, pressure });

        Assert.NotNull(stop);
        Assert.Equal(rate.Id, stop.Hits[0].BreakpointId);
        Assert.Equal(pressure.Id, stop.Hits[1].BreakpointId);
    }

    [Fact]
    public void RunToBreakpoints_ContinueFindsNextStop()
    {
        var session = CreateSession(
            (8500, 72),
            (9100, 68),
            (9300, 55),
            (8900, 52));

        var breakpoints = new[]
        {
            PressureAbove9000(),
            RateBelow60()
        };

        var first = session.RunToBreakpoints(breakpoints);
        var second = session.RunToBreakpoints(breakpoints);
        var third = session.RunToBreakpoints(breakpoints);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);

        Assert.Equal(1, first.Position.SequenceNumber);
        Assert.Equal(2, second.Position.SequenceNumber);
        Assert.Equal(3, third.Position.SequenceNumber);

        Assert.Single(first.Hits);
        Assert.Equal(2, second.Hits.Count);
        Assert.Single(third.Hits);
    }

    [Fact]
    public void RunToBreakpoints_NoMatch_ReturnsNullAtEnd()
    {
        var session = CreateSession(
            (8500, 72),
            (8600, 70),
            (8700, 68));

        var stop = session.RunToBreakpoints(
            new[]
            {
                PressureAbove9000(),
                RateBelow60()
            });

        Assert.Null(stop);
        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoints_RejectsEmptyCollection()
    {
        var session = CreateSession((8500, 72));

        Assert.Throws<ArgumentException>(() =>
            session.RunToBreakpoints(
                Array.Empty<ConditionalBreakpoint>()));

        Assert.Equal(0, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoints_RejectsDuplicateIds()
    {
        var session = CreateSession((8500, 72));
        var breakpoint = PressureAbove9000();

        Assert.Throws<ArgumentException>(() =>
            session.RunToBreakpoints(
                new[] { breakpoint, breakpoint }));

        Assert.Equal(0, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoints_RejectsNullCollection()
    {
        var session = CreateSession((8500, 72));

        Assert.Throws<ArgumentNullException>(() =>
            session.RunToBreakpoints((BreakpointCollection)null!));
    }

    [Fact]
    public void RunToBreakpoints_RejectsNullEntries()
    {
        var session = CreateSession((8500, 72));

        Assert.Throws<ArgumentException>(() =>
            session.RunToBreakpoints(
                new ConditionalBreakpoint[]
                {
                    PressureAbove9000(),
                    null!
                }));
    }

    [Fact]
    public void RunToBreakpoints_AfterReset_IsReproducible()
    {
        var session = CreateSession(
            (8500, 72),
            (9300, 55));

        var breakpoints = new[]
        {
            PressureAbove9000(),
            RateBelow60()
        };

        var first = session.RunToBreakpoints(breakpoints);

        session.Reset();

        var second = session.RunToBreakpoints(breakpoints);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Position, second.Position);
        Assert.Equal(first.Hits, second.Hits);
    }

    [Fact]
    public void BreakpointStop_RejectsEmptyHits()
    {
        var position = new SamplePosition(_recordingId, 1);

        Assert.Throws<ArgumentException>(() =>
            new BreakpointStop(
                position,
                new TelemetryTimestamp(100),
                Array.Empty<BreakpointHit>()));
    }
}
