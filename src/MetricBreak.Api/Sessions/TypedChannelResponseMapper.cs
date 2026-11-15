using MetricBreak.Domain.Recordings;

namespace MetricBreak.Api.Sessions;

internal static class TypedChannelResponseMapper
{
    public static IReadOnlyDictionary<Guid, TypedChannelValueResponse> Map(TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var result = new Dictionary<Guid, TypedChannelValueResponse>();
        foreach (var (channelId, value) in sample.TypedValues)
        {
            result.Add(channelId, value switch
            {
                ChannelValue.Numeric numeric => new("Numeric",
                    double.IsFinite(numeric.Value) ? numeric.Value : null),
                ChannelValue.Boolean boolean => new("Boolean", boolean.Value),
                ChannelValue.Categorical categorical => new("Categorical", categorical.Value),
                null => new("Missing", null),
                _ => throw new InvalidOperationException("Unsupported telemetry value type.")
            });
        }
        return result;
    }
}
