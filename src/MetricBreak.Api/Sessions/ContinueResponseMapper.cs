using MetricBreak.Domain.Breakpoints;

namespace MetricBreak.Api.Sessions;

public static class ContinueResponseMapper
{
    public static BreakpointStopResponse Map(
        BreakpointStop stop)
    {
        ArgumentNullException.ThrowIfNull(stop);

        return new BreakpointStopResponse(
            Sequence: stop.Position.SequenceNumber,
            TimestampNanoseconds: stop.Timestamp.UnixNanoseconds,
            Hits: stop.Hits
                .Select(hit => new BreakpointHitResponse(
                    BreakpointId: hit.BreakpointId,
                    ChannelId: hit.ChannelId,
                    ObservedValue: hit.ObservedValue,
                    Operator: hit.Operator.ToString(),
                    Threshold: hit.Threshold))
                .ToArray());
    }
}
