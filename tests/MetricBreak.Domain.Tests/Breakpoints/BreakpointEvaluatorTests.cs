
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class BreakpointEvaluatorTests
{
    private static readonly Guid _channelId = Guid.NewGuid();

    private static TelemetrySample CreateSample(double? value)
    {
        return new TelemetrySample(
            0,
            new TelemetryTimestamp(0),
            new Dictionary<Guid, double?>
            {
                [_channelId] = value
            });
    }

    private static ConditionalBreakpoint CreateBreakpoint(
        ComparisonOperator comparisonOperator,
        double threshold = 9000)
    {
        return new ConditionalBreakpoint(
            Guid.NewGuid(),
            _channelId,
            comparisonOperator,
            threshold);
    }

    [Theory]
    [InlineData(8500, false)]
    [InlineData(9000, false)]
    [InlineData(9100, true)]
    public void GreaterThan_EvaluatesCorrectly(
        double value,
        bool expected)
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThan);

        Assert.Equal(
            expected,
            BreakpointEvaluator.Evaluate(
                breakpoint,
                CreateSample(value)));
    }

    [Theory]
    [InlineData(8500, false)]
    [InlineData(9000, true)]
    [InlineData(9100, true)]
    public void GreaterThanOrEqual_EvaluatesCorrectly(
        double value,
        bool expected)
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThanOrEqual);

        Assert.Equal(
            expected,
            BreakpointEvaluator.Evaluate(
                breakpoint,
                CreateSample(value)));
    }

    [Theory]
    [InlineData(8500, true)]
    [InlineData(9000, false)]
    [InlineData(9100, false)]
    public void LessThan_EvaluatesCorrectly(
        double value,
        bool expected)
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.LessThan);

        Assert.Equal(
            expected,
            BreakpointEvaluator.Evaluate(
                breakpoint,
                CreateSample(value)));
    }

    [Theory]
    [InlineData(8500, true)]
    [InlineData(9000, true)]
    [InlineData(9100, false)]
    public void LessThanOrEqual_EvaluatesCorrectly(
        double value,
        bool expected)
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.LessThanOrEqual);

        Assert.Equal(
            expected,
            BreakpointEvaluator.Evaluate(
                breakpoint,
                CreateSample(value)));
    }

    [Theory]
    [InlineData(8500, false)]
    [InlineData(9000, true)]
    [InlineData(9100, false)]
    public void Equal_EvaluatesCorrectly(
        double value,
        bool expected)
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.Equal);

        Assert.Equal(
            expected,
            BreakpointEvaluator.Evaluate(
                breakpoint,
                CreateSample(value)));
    }

    [Theory]
    [InlineData(8500, true)]
    [InlineData(9000, false)]
    [InlineData(9100, true)]
    public void NotEqual_EvaluatesCorrectly(
        double value,
        bool expected)
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.NotEqual);

        Assert.Equal(
            expected,
            BreakpointEvaluator.Evaluate(
                breakpoint,
                CreateSample(value)));
    }

    [Fact]
    public void NullValue_DoesNotMatch()
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThan);

        Assert.False(BreakpointEvaluator.Evaluate(
            breakpoint,
            CreateSample(null)));
    }

    [Fact]
    public void MissingChannel_DoesNotMatch()
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThan);

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(0),
            new Dictionary<Guid, double?>());

        Assert.False(BreakpointEvaluator.Evaluate(
            breakpoint, sample));
    }

    [Fact]
    public void NaNValue_DoesNotMatch()
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.NotEqual);

        Assert.False(BreakpointEvaluator.Evaluate(
            breakpoint,
            CreateSample(double.NaN)));
    }

    [Fact]
    public void Constructor_WithEmptyBreakpointId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ConditionalBreakpoint(
                Guid.Empty,
                _channelId,
                ComparisonOperator.GreaterThan,
                9000));
    }

    [Fact]
    public void Constructor_WithEmptyChannelId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ConditionalBreakpoint(
                Guid.NewGuid(),
                Guid.Empty,
                ComparisonOperator.GreaterThan,
                9000));
    }

    [Fact]
    public void Constructor_WithInvalidOperator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConditionalBreakpoint(
                Guid.NewGuid(),
                _channelId,
                (ComparisonOperator)999,
                9000));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithNonFiniteThreshold_Throws(
        double threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConditionalBreakpoint(
                Guid.NewGuid(),
                _channelId,
                ComparisonOperator.GreaterThan,
                threshold));
    }

    [Fact]
    public void Evaluate_WithNullBreakpoint_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BreakpointEvaluator.Evaluate(
                null!,
                CreateSample(9000)));
    }

    [Fact]
    public void Evaluate_WithNullSample_Throws()
    {
        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThan);

        Assert.Throws<ArgumentNullException>(() =>
            BreakpointEvaluator.Evaluate(
                breakpoint,
                null!));
    }
}
