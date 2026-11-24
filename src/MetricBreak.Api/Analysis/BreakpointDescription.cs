using System.Globalization;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Api.Analysis;

internal static class BreakpointDescription
{
    public static string Describe(
        IBreakpoint breakpoint,
        IReadOnlyDictionary<Guid, ChannelDefinition> channels)
    {
        return breakpoint switch
        {
            ConditionalBreakpoint conditional => DescribeConditional(conditional, channels),
            MissingDataBreakpoint missing => DescribeMissing(missing, channels),
            TimelineBreakpoint timeline => timeline.Kind == TimelineMarkerKind.Stage
                ? $"Stage · {timeline.Name ?? "Any"}"
                : $"Event · {timeline.Name ?? "Any"}",
            CompositeBreakpoint composite => string.Join(
                composite.LogicalOperator == LogicalOperator.And ? " AND " : " OR ",
                composite.Conditions.Select(condition => DescribeCondition(condition, channels))),
            _ => breakpoint.Id.ToString(),
        };
    }

    private static string DescribeConditional(
        ConditionalBreakpoint breakpoint,
        IReadOnlyDictionary<Guid, ChannelDefinition> channels) =>
        $"{Name(breakpoint.ChannelId, channels)} {Symbol(breakpoint.Operator)}{Expected(breakpoint.Operator, breakpoint.ExpectedValue, breakpoint.Threshold)}";

    private static string DescribeCondition(
        BreakpointCondition condition,
        IReadOnlyDictionary<Guid, ChannelDefinition> channels) =>
        $"{Name(condition.ChannelId, channels)} {Symbol(condition.Operator)}{Expected(condition.Operator, condition.ExpectedValue, condition.Threshold)}";

    private static string DescribeMissing(
        MissingDataBreakpoint breakpoint,
        IReadOnlyDictionary<Guid, ChannelDefinition> channels)
    {
        var scope = breakpoint.ChannelId is Guid channelId
            ? Name(channelId, channels)
            : "Recording";
        return $"{scope} missing > {breakpoint.MinimumSeconds.ToString(CultureInfo.InvariantCulture)}s";
    }

    private static string Name(Guid channelId, IReadOnlyDictionary<Guid, ChannelDefinition> channels) =>
        channels.TryGetValue(channelId, out var channel) ? channel.Name : channelId.ToString();

    private static string Symbol(ComparisonOperator op) => op switch
    {
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "<=",
        ComparisonOperator.Equal => "==",
        ComparisonOperator.NotEqual => "!=",
        ComparisonOperator.Changed => "changed",
        ComparisonOperator.RisingEdge => "rises above",
        ComparisonOperator.FallingEdge => "falls below",
        _ => op.ToString(),
    };

    private static string Expected(
        ComparisonOperator op,
        ChannelValue? expected,
        double threshold)
    {
        if (op == ComparisonOperator.Changed)
            return "";

        var text = expected switch
        {
            ChannelValue.Boolean boolean => boolean.Value ? "true" : "false",
            ChannelValue.Categorical categorical => categorical.Value,
            ChannelValue.Numeric numeric => numeric.Value.ToString(CultureInfo.InvariantCulture),
            _ => threshold.ToString(CultureInfo.InvariantCulture),
        };
        return " " + text;
    }
}
