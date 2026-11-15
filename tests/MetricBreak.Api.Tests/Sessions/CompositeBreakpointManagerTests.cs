
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class CompositeBreakpointManagerTests
{
    private const string CsvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,15\n" +
        "2026-11-08T10:00:03Z,9700,12\n";

    [Fact]
    public void AddComposite_ValidConditions_Succeeds()
    {
        var fixture = CreateSession();

        var found = fixture.Manager.TryAddCompositeBreakpoint(
            fixture.SessionId,
            LogicalOperator.And,
            CreateConditions(fixture),
            out var breakpoint,
            out var unknownChannel);

        Assert.True(found);
        Assert.False(unknownChannel);
        Assert.NotNull(breakpoint);
        Assert.NotEqual(Guid.Empty, breakpoint.Id);
        Assert.Equal(LogicalOperator.And, breakpoint.LogicalOperator);
        Assert.Equal(2, breakpoint.Conditions.Count);
    }

    [Fact]
    public void AddComposite_UnknownChannel_IsRejected()
    {
        var fixture = CreateSession();

        var conditions = new[]
        {
            new BreakpointCondition(
                Guid.NewGuid(),
                ComparisonOperator.GreaterThan,
                9000),
            new BreakpointCondition(
                fixture.RateChannelId,
                ComparisonOperator.LessThan,
                20)
        };

        var found = fixture.Manager.TryAddCompositeBreakpoint(
            fixture.SessionId,
            LogicalOperator.And,
            conditions,
            out var breakpoint,
            out var unknownChannel);

        Assert.True(found);
        Assert.True(unknownChannel);
        Assert.Null(breakpoint);
    }

    [Fact]
    public void AddComposite_UnknownSession_ReturnsFalse()
    {
        var fixture = CreateSession();

        var found = fixture.Manager.TryAddCompositeBreakpoint(
            Guid.NewGuid(),
            LogicalOperator.And,
            CreateConditions(fixture),
            out var breakpoint,
            out var unknownChannel);

        Assert.False(found);
        Assert.False(unknownChannel);
        Assert.Null(breakpoint);
    }

    [Fact]
    public void AddComposite_OneCondition_Throws()
    {
        var fixture = CreateSession();

        var conditions = new[]
        {
            new BreakpointCondition(
                fixture.PressureChannelId,
                ComparisonOperator.GreaterThan,
                9000)
        };

        Assert.Throws<ArgumentException>(() =>
            fixture.Manager.TryAddCompositeBreakpoint(
                fixture.SessionId,
                LogicalOperator.And,
                conditions,
                out _,
                out _));
    }

    [Fact]
    public void AddComposite_InvalidLogicalOperator_Throws()
    {
        var fixture = CreateSession();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.Manager.TryAddCompositeBreakpoint(
                fixture.SessionId,
                (LogicalOperator)999,
                CreateConditions(fixture),
                out _,
                out _));
    }

    [Fact]
    public void AddComposite_DoesNotRegisterPartialBreakpoint()
    {
        var fixture = CreateSession();

        var conditions = new[]
        {
            new BreakpointCondition(
                fixture.PressureChannelId,
                ComparisonOperator.GreaterThan,
                9000),
            new BreakpointCondition(
                Guid.NewGuid(),
                ComparisonOperator.LessThan,
                20)
        };

        fixture.Manager.TryAddCompositeBreakpoint(
            fixture.SessionId,
            LogicalOperator.And,
            conditions,
            out _,
            out var unknownChannel);

        Assert.True(unknownChannel);

        Assert.True(fixture.Manager.TryGetCompositeBreakpoints(
            fixture.SessionId,
            out var registered));

        Assert.NotNull(registered);
        Assert.Empty(registered);
    }

    [Fact]
    public void GetComposite_ReturnsRegisteredBreakpoint()
    {
        var fixture = CreateSession();
        var breakpointId = AddComposite(fixture);

        Assert.True(fixture.Manager.TryGetCompositeBreakpoints(
            fixture.SessionId,
            out var registered));

        Assert.NotNull(registered);

        var item = Assert.Single(registered);

        Assert.Equal(breakpointId, item.Breakpoint.Id);
        Assert.True(item.IsEnabled);
        Assert.Equal(2, item.Breakpoint.Conditions.Count);
    }

    [Fact]
    public void DisableComposite_UpdatesEnabledState()
    {
        var fixture = CreateSession();
        var breakpointId = AddComposite(fixture);

        Assert.True(fixture.Manager.TrySetBreakpointEnabled(
            fixture.SessionId,
            breakpointId,
            false,
            out var updated));

        Assert.True(updated);

        Assert.True(fixture.Manager.TryGetCompositeBreakpoints(
            fixture.SessionId,
            out var registered));

        Assert.NotNull(registered);
        Assert.False(Assert.Single(registered).IsEnabled);
    }

    [Fact]
    public void EnableComposite_UpdatesEnabledState()
    {
        var fixture = CreateSession();
        var breakpointId = AddComposite(fixture);

        Assert.True(fixture.Manager.TrySetBreakpointEnabled(
            fixture.SessionId,
            breakpointId,
            false,
            out var disabled));

        Assert.True(disabled);

        Assert.True(fixture.Manager.TrySetBreakpointEnabled(
            fixture.SessionId,
            breakpointId,
            true,
            out var enabled));

        Assert.True(enabled);

        Assert.True(fixture.Manager.TryGetCompositeBreakpoints(
            fixture.SessionId,
            out var registered));

        Assert.NotNull(registered);
        Assert.True(Assert.Single(registered).IsEnabled);
    }

    [Fact]
    public void RemoveComposite_RemovesRegistration()
    {
        var fixture = CreateSession();
        var breakpointId = AddComposite(fixture);

        Assert.True(fixture.Manager.TryRemoveBreakpoint(
            fixture.SessionId,
            breakpointId,
            out var removed));

        Assert.True(removed);

        Assert.True(fixture.Manager.TryGetCompositeBreakpoints(
            fixture.SessionId,
            out var registered));

        Assert.NotNull(registered);
        Assert.Empty(registered);
    }

    [Fact]
    public void Continue_AndComposite_StopsWhenBothConditionsMatch()
    {
        var fixture = CreateSession();
        var breakpointId = AddComposite(fixture);

        var found = fixture.Manager.TryContinueWithSnapshot(
            fixture.SessionId,
            out var hadActiveBreakpoints,
            out var stop,
            out var snapshot);

        Assert.True(found);
        Assert.True(hadActiveBreakpoints);
        Assert.NotNull(stop);
        Assert.NotNull(snapshot);

        Assert.Equal(2, stop.Position.SequenceNumber);

        var hit = Assert.Single(stop.Hits);

        Assert.Equal(breakpointId, hit.BreakpointId);
        Assert.True(hit.IsComposite);
        Assert.Equal(LogicalOperator.And, hit.LogicalOperator);

        Assert.Equal(2, hit.Conditions.Count);
        Assert.All(hit.Conditions, condition =>
            Assert.True(condition.Matched));
    }

    private static Guid AddComposite(SessionFixture fixture)
    {
        var found = fixture.Manager.TryAddCompositeBreakpoint(
            fixture.SessionId,
            LogicalOperator.And,
            CreateConditions(fixture),
            out var breakpoint,
            out var unknownChannel);

        Assert.True(found);
        Assert.False(unknownChannel);
        Assert.NotNull(breakpoint);

        return breakpoint.Id;
    }

    private static BreakpointCondition[] CreateConditions(
        SessionFixture fixture)
    {
        return
        [
            new BreakpointCondition(
                fixture.PressureChannelId,
                ComparisonOperator.GreaterThan,
                9000),
            new BreakpointCondition(
                fixture.RateChannelId,
                ComparisonOperator.LessThan,
                20)
        ];
    }

    private static SessionFixture CreateSession()
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(CsvContent);
        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Composite Breakpoint Test",
            new RecordingSource(
                "composite-test.csv",
                "composite-test-hash",
                CsvContent.Length),
            DateTimeOffset.UtcNow);

        var telemetryReader = new CsvTelemetryReader(
            recordingId,
            parsed);

        var imported = new ImportedCsvRecording(
            recording,
            parsed.Channels,
            telemetryReader);

        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(imported);

        var pressureId = parsed.Channels
            .Single(channel => channel.Name == "Pressure")
            .Id;

        var rateId = parsed.Channels
            .Single(channel => channel.Name == "Rate")
            .Id;

        return new SessionFixture(
            manager,
            sessionId,
            pressureId,
            rateId);
    }

    private sealed record SessionFixture(
        ReplaySessionManager Manager,
        Guid SessionId,
        Guid PressureChannelId,
        Guid RateChannelId);
}
