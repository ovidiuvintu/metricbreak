
namespace MetricBreak.Api.Breakpoints;

public sealed record BreakpointsResponse(
    IReadOnlyList<BreakpointResponse> Breakpoints,
    IReadOnlyList<CompositeBreakpointResponse> CompositeBreakpoints)
{
    public BreakpointsResponse(
        IReadOnlyList<BreakpointResponse> breakpoints)
        : this(
            breakpoints,
            Array.Empty<CompositeBreakpointResponse>())
    {
    }
}

