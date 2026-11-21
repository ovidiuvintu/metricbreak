using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class TypedConditionEvaluatorTests
{
    private static TelemetrySample Sample(long sequence, Guid id, ChannelValue? value) =>
        TelemetrySample.FromTypedValues(sequence, default, new Dictionary<Guid, ChannelValue?> { [id] = value });

    [Fact]
    public void Categorical_equality_is_ordinal_and_type_safe()
    {
        var id = Guid.NewGuid();
        var sample = Sample(0, id, new ChannelValue.Categorical("ON"));
        Assert.True(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.Equal, 0,
            new ChannelValue.Categorical("ON"), sample, null));
        Assert.False(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.Equal, 0,
            new ChannelValue.Categorical("on"), sample, null));
        Assert.False(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.Equal, 0,
            new ChannelValue.Boolean(true), sample, null));
    }

    [Fact]
    public void Boolean_equality_and_missing_are_deterministic()
    {
        var id = Guid.NewGuid();
        Assert.True(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.Equal, 0,
            new ChannelValue.Boolean(true), Sample(0, id, new ChannelValue.Boolean(true)), null));
        Assert.False(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.NotEqual, 0,
            new ChannelValue.Boolean(true), Sample(0, id, null), null));
    }

    [Fact]
    public void Changed_does_not_bridge_sequence_gaps()
    {
        var id = Guid.NewGuid();
        Assert.False(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.Changed, 0, null,
            Sample(3, id, new ChannelValue.Categorical("ON")),
            Sample(1, id, new ChannelValue.Categorical("OFF"))));
        Assert.True(TypedConditionEvaluator.Evaluate(id, ComparisonOperator.Changed, 0, null,
            Sample(2, id, new ChannelValue.Categorical("ON")),
            Sample(1, id, new ChannelValue.Categorical("OFF"))));
    }
}
