
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class CompositeBreakpointReplayTests
{
    private readonly Guid _recordingId = Guid.NewGuid();
    private readonly Guid _pressureId = Guid.NewGuid();
    private readonly Guid _rateId = Guid.NewGuid();

    private ReplaySession CreateSession()
    {
        var recording = new Recording(
            _recordingId,
            "Composite Breakpoint Test",
            new RecordingSource("test.csv", "test-hash", 100),
            DateTimeOffset.UtcNow);

        var measurements = new (double Pressure, double Rate)[]
        {
            (8500, 72), // Sequence 0
            (8800, 65), // Sequence 1
            (9300, 55), // Sequence 2
            (9500, 15), // Sequence 3
            (8700, 70)  // Sequence 4
        };

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

    private CompositeBreakpoint CreateComposite(
        LogicalOperator logicalOperator)
    {
        return new CompositeBreakpoint(
            Guid.NewGuid(),
            logicalOperator,
            new[]
            {
                new BreakpointCondition(
                    _pressureId,
                    ComparisonOperator.GreaterThan,
                    9000),

                new BreakpointCondition(
                    _rateId,
                    ComparisonOperator.LessThan,
                    20)
            });
    }

    [Fact]
    public void Forward_AndComposite_StopsWhenBothMatch()
    {
        var session = CreateSession();
        var composite = CreateComposite(LogicalOperator.And);

        var collection = new BreakpointCollection();
        collection.Add(composite);

        var stop = session.RunToBreakpoints(collection);

        Assert.NotNull(stop);
        Assert.Equal(3, stop.Position.SequenceNumber);

        var hit = Assert.Single(stop.Hits);

        Assert.Equal(composite.Id, hit.BreakpointId);
        Assert.True(hit.IsComposite);
        Assert.Equal(LogicalOperator.And, hit.LogicalOperator);
        Assert.Equal(2, hit.Conditions.Count);

        Assert.All(
            hit.Conditions,
            condition => Assert.True(condition.Matched));
    }

    [Fact]
    public void Forward_OrComposite_StopsWhenOneMatches()
    {
        var session = CreateSession();
        var composite = CreateComposite(LogicalOperator.Or);

        var collection = new BreakpointCollection();
        collection.Add(composite);

        var stop = session.RunToBreakpoints(collection);

        Assert.NotNull(stop);
        Assert.Equal(2, stop.Position.SequenceNumber);

        var hit = Assert.Single(stop.Hits);

        Assert.True(hit.IsComposite);
        Assert.Equal(LogicalOperator.Or, hit.LogicalOperator);
        Assert.Equal(2, hit.Conditions.Count);

        Assert.True(hit.Conditions[0].Matched);
        Assert.False(hit.Conditions[1].Matched);
    }

    [Fact]
    public void Backward_AndComposite_StopsAtMatchingSample()
    {
        var session = CreateSession();
        session.Seek(4);

        var composite = CreateComposite(LogicalOperator.And);

        var collection = new BreakpointCollection();
        collection.Add(composite);

        var stop = session.RunBackwardToBreakpoints(collection);

        Assert.NotNull(stop);
        Assert.Equal(3, stop.Position.SequenceNumber);

        var hit = Assert.Single(stop.Hits);

        Assert.Equal(composite.Id, hit.BreakpointId);
        Assert.True(hit.IsComposite);
    }

    [Fact]
    public void DisabledComposite_DoesNotAdvanceReplay()
    {
        var session = CreateSession();
        var composite = CreateComposite(LogicalOperator.And);

        var collection = new BreakpointCollection();
        collection.Add(composite);
        collection.Disable(composite.Id);

        var initialPosition = session.Position;
        var initialState = session.ExecutionState;

        var stop = session.RunToBreakpoints(collection);

        Assert.Null(stop);
        Assert.Equal(initialPosition, session.Position);
        Assert.Equal(initialState, session.ExecutionState);
    }

    [Fact]
    public void Composite_AfterReset_ProducesEqualStops()
    {
        var session = CreateSession();
        var composite = CreateComposite(LogicalOperator.And);

        var collection = new BreakpointCollection();
        collection.Add(composite);

        var first = session.RunToBreakpoints(collection);

        session.Reset();

        var second = session.RunToBreakpoints(collection);

        Assert.NotNull(first);
        Assert.NotNull(second);

        Assert.Equal(first, second);
        Assert.Equal(
            first.GetHashCode(),
            second.GetHashCode());
    }

    [Fact]
    public void MultipleMatchingBreakpoints_ProduceSeparateHits()
    {
        var session = CreateSession();

        var composite = CreateComposite(LogicalOperator.And);

        var conditional = new ConditionalBreakpoint(
            Guid.NewGuid(),
            _pressureId,
            ComparisonOperator.GreaterThan,
            9000);

        var collection = new BreakpointCollection();

        collection.Add(composite);
        collection.Add(conditional);

        session.Seek(2);

        var stop = session.RunToBreakpoints(collection);

        Assert.NotNull(stop);
        Assert.Equal(3, stop.Position.SequenceNumber);
        Assert.Equal(2, stop.Hits.Count);

        Assert.Equal(
            composite.Id,
            stop.Hits[0].BreakpointId);

        Assert.Equal(
            conditional.Id,
            stop.Hits[1].BreakpointId);

        Assert.True(stop.Hits[0].IsComposite);
        Assert.False(stop.Hits[1].IsComposite);
    }
}
