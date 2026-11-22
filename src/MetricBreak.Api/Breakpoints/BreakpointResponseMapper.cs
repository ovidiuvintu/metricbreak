
using MetricBreak.Domain.Breakpoints;

namespace MetricBreak.Api.Breakpoints;

public static class BreakpointResponseMapper
{
    public static BreakpointResponse Map(
        ConditionalBreakpoint breakpoint,
        bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return new BreakpointResponse(
            BreakpointId: breakpoint.Id,
            ChannelId: breakpoint.ChannelId,
            Operator: breakpoint.Operator.ToString(),
            Threshold: breakpoint.Threshold,
            IsEnabled: isEnabled,
            Value: BreakpointValueParser.ToWire(breakpoint.ExpectedValue));
    }

    public static BreakpointResponse Map(
        MissingDataBreakpoint breakpoint,
        bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return new BreakpointResponse(
            BreakpointId: breakpoint.Id,
            ChannelId: breakpoint.ChannelId ?? Guid.Empty,
            Operator: MissingDataBreakpoint.OperatorName,
            Threshold: breakpoint.MinimumSeconds,
            IsEnabled: isEnabled);
    }
}

