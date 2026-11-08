
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class BreakpointCollectionTests
{
    private readonly Guid _channelId = Guid.NewGuid();

    private ConditionalBreakpoint CreateBreakpoint(
        double threshold = 9000)
    {
        return new ConditionalBreakpoint(
            Guid.NewGuid(),
            _channelId,
            ComparisonOperator.GreaterThan,
            threshold);
    }

    [Fact]
    public void Add_RegistersEnabledBreakpoint()
    {
        var collection = new BreakpointCollection();
        var breakpoint = CreateBreakpoint();

        collection.Add(breakpoint);

        Assert.Equal(1, collection.Count);
        Assert.Equal(1, collection.ActiveCount);
        Assert.True(collection.IsEnabled(breakpoint.Id));
        Assert.Equal(breakpoint, Assert.Single(collection.GetActive()));
    }

    [Fact]
    public void Add_DuplicateId_Throws()
    {
        var collection = new BreakpointCollection();
        var breakpoint = CreateBreakpoint();

        collection.Add(breakpoint);

        Assert.Throws<ArgumentException>(() =>
            collection.Add(breakpoint));

        Assert.Equal(1, collection.Count);
    }

    [Fact]
    public void Add_NullBreakpoint_Throws()
    {
        var collection = new BreakpointCollection();

        Assert.Throws<ArgumentNullException>(() =>
            collection.Add(null!));
    }

    [Fact]
    public void Disable_RemovesBreakpointFromActiveSet()
    {
        var collection = new BreakpointCollection();
        var breakpoint = CreateBreakpoint();

        collection.Add(breakpoint);

        Assert.True(collection.Disable(breakpoint.Id));
        Assert.False(collection.IsEnabled(breakpoint.Id));
        Assert.Equal(1, collection.Count);
        Assert.Equal(0, collection.ActiveCount);
        Assert.Empty(collection.GetActive());
    }

    [Fact]
    public void Enable_ReactivatesBreakpoint()
    {
        var collection = new BreakpointCollection();
        var breakpoint = CreateBreakpoint();

        collection.Add(breakpoint);
        collection.Disable(breakpoint.Id);

        Assert.True(collection.Enable(breakpoint.Id));
        Assert.True(collection.IsEnabled(breakpoint.Id));
        Assert.Single(collection.GetActive());
    }

    [Fact]
    public void Remove_UnregistersBreakpoint()
    {
        var collection = new BreakpointCollection();
        var breakpoint = CreateBreakpoint();

        collection.Add(breakpoint);

        Assert.True(collection.Remove(breakpoint.Id));
        Assert.False(collection.Remove(breakpoint.Id));
        Assert.Equal(0, collection.Count);
        Assert.Empty(collection.GetActive());
    }

    [Fact]
    public void GetActive_PreservesRegistrationOrder()
    {
        var collection = new BreakpointCollection();

        var first = CreateBreakpoint(9000);
        var second = CreateBreakpoint(9500);
        var third = CreateBreakpoint(10000);

        collection.Add(first);
        collection.Add(second);
        collection.Add(third);

        collection.Disable(second.Id);

        Assert.Equal(
            new[] { first.Id, third.Id },
            collection.GetActive().Select(b => b.Id));

        collection.Enable(second.Id);

        Assert.Equal(
            new[] { first.Id, second.Id, third.Id },
            collection.GetActive().Select(b => b.Id));
    }

    [Fact]
    public void RemoveAndReAdd_MovesBreakpointToEnd()
    {
        var collection = new BreakpointCollection();

        var first = CreateBreakpoint();
        var second = CreateBreakpoint();

        collection.Add(first);
        collection.Add(second);

        collection.Remove(first.Id);
        collection.Add(first);

        Assert.Equal(
            new[] { second.Id, first.Id },
            collection.GetActive().Select(b => b.Id));
    }

    [Fact]
    public void GetActive_ReturnsIndependentSnapshot()
    {
        var collection = new BreakpointCollection();
        var breakpoint = CreateBreakpoint();

        collection.Add(breakpoint);

        var snapshot = collection.GetActive();

        collection.Disable(breakpoint.Id);

        Assert.Single(snapshot);
        Assert.Empty(collection.GetActive());
    }

    [Fact]
    public void Clear_RemovesAllBreakpoints()
    {
        var collection = new BreakpointCollection();

        collection.Add(CreateBreakpoint());
        collection.Add(CreateBreakpoint());

        collection.Clear();

        Assert.Equal(0, collection.Count);
        Assert.Equal(0, collection.ActiveCount);
        Assert.Empty(collection.GetActive());
    }

    [Fact]
    public void UnknownBreakpoint_ReturnsFalse()
    {
        var collection = new BreakpointCollection();
        var unknownId = Guid.NewGuid();

        Assert.False(collection.Enable(unknownId));
        Assert.False(collection.Disable(unknownId));
        Assert.False(collection.Remove(unknownId));
        Assert.False(collection.IsEnabled(unknownId));
    }

    [Fact]
    public void RunToBreakpoints_UsesOnlyEnabledDefinitions()
    {
        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Breakpoint Management Test",
            new RecordingSource("test.csv", "test-hash", 100),
            DateTimeOffset.UtcNow);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                new TelemetryTimestamp(0),
                new Dictionary<Guid, double?>
                {
                    [_channelId] = 8500
                }),
            new TelemetrySample(
                1,
                new TelemetryTimestamp(1_000_000_000),
                new Dictionary<Guid, double?>
                {
                    [_channelId] = 9200
                }),
            new TelemetrySample(
                2,
                new TelemetryTimestamp(2_000_000_000),
                new Dictionary<Guid, double?>
                {
                    [_channelId] = 9800
                })
        };

        var reader = new InMemoryTelemetryReader(
            recordingId, samples);

        var session = new ReplaySession(recording, reader);

        var collection = new BreakpointCollection();
        var lowThreshold = CreateBreakpoint(9000);
        var highThreshold = CreateBreakpoint(9500);

        collection.Add(lowThreshold);
        collection.Add(highThreshold);

        collection.Disable(lowThreshold.Id);

        var stop = session.RunToBreakpoints(collection);

        Assert.NotNull(stop);
        Assert.Equal(2, stop.Position.SequenceNumber);
        Assert.Equal(
            highThreshold.Id,
            Assert.Single(stop.Hits).BreakpointId);
    }

    [Fact]
    public void RunToBreakpoints_NoEnabledBreakpoints_DoesNotMove()
    {
        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Empty Breakpoint Set",
            new RecordingSource("test.csv", "test-hash", 100),
            DateTimeOffset.UtcNow);

        var reader = new InMemoryTelemetryReader(
            recordingId,
            new[]
            {
                new TelemetrySample(
                    0,
                    new TelemetryTimestamp(0),
                    new Dictionary<Guid, double?>()),
                new TelemetrySample(
                    1,
                    new TelemetryTimestamp(100),
                    new Dictionary<Guid, double?>())
            });

        var session = new ReplaySession(recording, reader);
        var collection = new BreakpointCollection();

        Assert.Null(session.RunToBreakpoints(collection));
        Assert.Equal(0, session.Position.SequenceNumber);
    }
}
