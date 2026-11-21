using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

public static class BreakpointOperatorValidation
{
    public static void Validate(ChannelValueType type, ComparisonOperator op)
    {
        if (!Enum.IsDefined(op)) throw new ArgumentOutOfRangeException(nameof(op));
        bool supported = type switch
        {
            ChannelValueType.Numeric => true,
            ChannelValueType.Boolean => op is ComparisonOperator.Equal or ComparisonOperator.NotEqual or ComparisonOperator.Changed or
                ComparisonOperator.RisingEdge or ComparisonOperator.FallingEdge,
            ChannelValueType.Categorical => op is ComparisonOperator.Equal or ComparisonOperator.NotEqual or ComparisonOperator.Changed,
            _ => false
        };
        if (!supported)
            throw new ArgumentException($"Operator {op} is not supported for {type} channels.", nameof(op));
    }
}
