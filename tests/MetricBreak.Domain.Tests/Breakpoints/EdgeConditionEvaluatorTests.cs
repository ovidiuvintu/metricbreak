using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class EdgeConditionEvaluatorTests
{
    [Theory]
    [InlineData(99, 101, true)]
    [InlineData(100, 101, true)]
    [InlineData(101, 102, false)]
    [InlineData(99, 100, false)]
    public void RisingNumeric_UsesStrictCrossing(double before, double after, bool expected)
        => Assert.Equal(expected, EdgeConditionEvaluator.Evaluate(ComparisonOperator.RisingEdge,
            new ChannelValue.Numeric(before), new ChannelValue.Numeric(after), 100));

    [Theory]
    [InlineData(101, 99, true)]
    [InlineData(100, 99, true)]
    [InlineData(99, 98, false)]
    [InlineData(101, 100, false)]
    public void FallingNumeric_UsesStrictCrossing(double before, double after, bool expected)
        => Assert.Equal(expected, EdgeConditionEvaluator.Evaluate(ComparisonOperator.FallingEdge,
            new ChannelValue.Numeric(before), new ChannelValue.Numeric(after), 100));

    [Fact]
    public void BooleanEdges_UseRecordedDirection()
    {
        Assert.True(EdgeConditionEvaluator.Evaluate(ComparisonOperator.RisingEdge,
            new ChannelValue.Boolean(false), new ChannelValue.Boolean(true)));
        Assert.True(EdgeConditionEvaluator.Evaluate(ComparisonOperator.FallingEdge,
            new ChannelValue.Boolean(true), new ChannelValue.Boolean(false)));
        Assert.False(EdgeConditionEvaluator.Evaluate(ComparisonOperator.RisingEdge,
            new ChannelValue.Boolean(true), new ChannelValue.Boolean(true)));
    }

    [Fact]
    public void CategoricalChanged_IsOrdinalAndCaseSensitive()
    {
        Assert.True(EdgeConditionEvaluator.Evaluate(ComparisonOperator.Changed,
            new ChannelValue.Categorical("ON"), new ChannelValue.Categorical("on")));
        Assert.False(EdgeConditionEvaluator.Evaluate(ComparisonOperator.Changed,
            new ChannelValue.Categorical("ON"), new ChannelValue.Categorical("ON")));
    }

    [Fact]
    public void MissingOrNonFiniteValues_DoNotCreateEdges()
    {
        Assert.False(EdgeConditionEvaluator.Evaluate(ComparisonOperator.Changed,
            null, new ChannelValue.Numeric(1)));
        Assert.False(EdgeConditionEvaluator.Evaluate(ComparisonOperator.RisingEdge,
            new ChannelValue.Numeric(double.NaN), new ChannelValue.Numeric(101), 100));
        Assert.False(EdgeConditionEvaluator.Evaluate(ComparisonOperator.FallingEdge,
            new ChannelValue.Numeric(double.PositiveInfinity), new ChannelValue.Numeric(99), 100));
    }

    [Fact]
    public void UnsupportedOperatorType_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => BreakpointOperatorValidation.Validate(
            ChannelValueType.Categorical, ComparisonOperator.RisingEdge));
        BreakpointOperatorValidation.Validate(ChannelValueType.Boolean, ComparisonOperator.FallingEdge);
    }
}
