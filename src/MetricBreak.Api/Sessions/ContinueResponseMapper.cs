using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

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
        if (hit.IsTimeline)
        {
            return new BreakpointHitResponse(
                BreakpointId: hit.BreakpointId,
                ChannelId: Guid.Empty,
                ObservedValue: 0,
                Operator: hit.MarkerKind == TimelineMarkerKind.Stage
                    ? TimelineBreakpoint.StageOperatorName
                    : TimelineBreakpoint.EventOperatorName,
                Threshold: 0)
            {
                ObservedText = hit.MarkerName,
            };
        }

        if (hit.IsMissingData)
        {
            return new BreakpointHitResponse(
                BreakpointId: hit.BreakpointId,
                ChannelId: hit.ChannelId,
                ObservedValue: hit.ObservedValue,
                Operator: MissingDataBreakpoint.OperatorName,
                Threshold: hit.Threshold);
        }

        if (!hit.IsComposite)
        {
            return new BreakpointHitResponse(
                BreakpointId: hit.BreakpointId,
                ChannelId: hit.ChannelId,
                ObservedValue: hit.ObservedValue,
                Operator: hit.Operator.ToString(),
                Threshold: hit.Threshold)
            {
                ObservedValueType = hit.TypedObservedValue switch
                {
                    ChannelValue.Numeric => "Numeric",
                    ChannelValue.Boolean => "Boolean",
                    ChannelValue.Categorical => "Categorical",
                    _ => null
                },
                ObservedText = hit.TypedObservedValue switch
                {
                    ChannelValue.Numeric n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ChannelValue.Boolean b => b.Value ? "true" : "false",
                    ChannelValue.Categorical c => c.Value,
                    _ => null
                }
            };
        }

        var representativeCondition = hit.Conditions
            .FirstOrDefault(condition => condition.Matched)
            ?? hit.Conditions[0];

        return new BreakpointHitResponse(
            BreakpointId: hit.BreakpointId,
            ChannelId: representativeCondition.ChannelId,
            ObservedValue: representativeCondition.ObservedValue ?? 0d,
            Operator: representativeCondition.Operator.ToString(),
            Threshold: representativeCondition.Threshold)
        {
            ObservedValueType = representativeCondition.TypedObservedValue switch
            {
                ChannelValue.Numeric => "Numeric",
                ChannelValue.Boolean => "Boolean",
                ChannelValue.Categorical => "Categorical",
                _ => null
            },
            ObservedText = representativeCondition.TypedObservedValue switch
            {
                ChannelValue.Numeric n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ChannelValue.Boolean b => b.Value ? "true" : "false",
                ChannelValue.Categorical c => c.Value,
                _ => null
            }
        };
    }
}
