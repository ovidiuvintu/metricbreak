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
                .Select(MapHit)
                .ToArray());
    }

    private static BreakpointHitResponse MapHit(
        BreakpointHit hit)
    {
        if (!hit.IsComposite)
        {
            return new BreakpointHitResponse(
                BreakpointId: hit.BreakpointId,
                ChannelId: hit.ChannelId,
                ObservedValue: hit.ObservedValue,
                Operator: hit.Operator.ToString(),
                Threshold: hit.Threshold);
        }

        var representativeCondition = hit.Conditions
            .FirstOrDefault(condition => condition.Matched)
            ?? hit.Conditions[0];

        return new BreakpointHitResponse(
            BreakpointId: hit.BreakpointId,
            ChannelId: representativeCondition.ChannelId,
            ObservedValue: representativeCondition.ObservedValue ?? 0d,
            Operator: representativeCondition.Operator.ToString(),
            Threshold: representativeCondition.Threshold);
    }
}
