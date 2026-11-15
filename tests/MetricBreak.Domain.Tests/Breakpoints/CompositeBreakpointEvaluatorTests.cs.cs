
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class CompositeBreakpointEvaluatorTests
{
    private readonly Guid _pressureId = Guid.NewGuid();
    private readonly Guid _rateId = Guid.NewGuid();
    private readonly Guid _recordingId = Guid.NewGuid();

    private CompositeBreakpoint CreateBreakpoint(
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

    private TelemetrySample CreateSample(
        double? pressure,
        double? rate)
    {
        return new TelemetrySample(
            1,
            new TelemetryTimestamp(1_000_000_000),
            new Dictionary<Guid, double?>
            {
                [_pressureId] = pressure,
                [_rateId] = rate
            });
    }

    [Fact]
    public void And_WhenBothConditionsMatch_ReturnsTrue()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.And);
        var sample = CreateSample(9500, 15);

        Assert.True(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void And_WhenOneConditionFails_ReturnsFalse()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.And);
        var sample = CreateSample(9500, 35);

        Assert.False(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void Or_WhenOneConditionMatches_ReturnsTrue()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.Or);
        var sample = CreateSample(9500, 35);

        Assert.True(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void Or_WhenNeitherConditionMatches_ReturnsFalse()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.Or);
        var sample = CreateSample(8500, 35);

        Assert.False(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void And_WithMissingMeasurement_ReturnsFalse()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.And);
        var sample = CreateSample(9500, null);

        Assert.False(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void Or_WithMissingMeasurement_CanStillMatch()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.Or);
        var sample = CreateSample(9500, null);

        Assert.True(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void CompositeHit_ProducesExactlyOneHit()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.And);
        var sample = CreateSample(9500, 15);

        var position = new SamplePosition(_recordingId, 1);

        var hit = BreakpointEvaluator.EvaluateHit(
            breakpoint,
            position,
            sample);

        Assert.NotNull(hit);
        Assert.Equal(breakpoint.Id, hit.BreakpointId);
        Assert.True(hit.IsComposite);
        Assert.Equal(LogicalOperator.And, hit.LogicalOperator);
        Assert.Equal(2, hit.Conditions.Count);
        Assert.All(hit.Conditions, condition =>
            Assert.True(condition.Matched));
    }

    [Fact]
    public void OrHit_PreservesFailedConditionDiagnostics()
    {
        var breakpoint = CreateBreakpoint(LogicalOperator.Or);
        var sample = CreateSample(9500, 35);

        var hit = BreakpointEvaluator.EvaluateHit(
            breakpoint,
            new SamplePosition(_recordingId, 1),
            sample);

        Assert.NotNull(hit);
        Assert.True(hit.Conditions[0].Matched);
        Assert.False(hit.Conditions[1].Matched);
        Assert.Equal(9500, hit.Conditions[0].ObservedValue);
        Assert.Equal(35, hit.Conditions[1].ObservedValue);
    }
}
