using MetricBreak.Domain.Breakpoints;

namespace MetricBreak.Api.Sessions;

public static class BreakpointResponseMapper
{
    public static BreakpointResponse Map(
        ConditionalBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return new BreakpointResponse(
            BreakpointId: breakpoint.Id,
            ChannelId: breakpoint.ChannelId,
            Operator: breakpoint.Operator.ToString(),
            Threshold: breakpoint.Threshold);
    }
}
