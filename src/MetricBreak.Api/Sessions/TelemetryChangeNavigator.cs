
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Api.Sessions;

/// <summary>Finds transitions using original recorded samples, without mutating the reader.</summary>
public static class TelemetryChangeNavigator
{
    public static long? Find(ITelemetryReader reader, Guid channelId, long currentSequence, bool forward)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (channelId == Guid.Empty) throw new ArgumentException("Channel ID is required.", nameof(channelId));
        if (currentSequence < 0 || currentSequence >= reader.SampleCount)
            throw new ArgumentOutOfRangeException(nameof(currentSequence));

        // A transition at N compares sample N with sample N-1, including Missing.
        var first = forward ? currentSequence + 1 : currentSequence;
        if (forward)
        {
            for (var n = first; n < reader.SampleCount; n++)
                if (Changed(reader, channelId, n)) return n;
        }
        else
        {
            // Backward navigation lands on the previous transition's resulting sample.
            // Exclude the transition at the current position, if any.
            for (var n = currentSequence - 1; n >= 1; n--)
                if (Changed(reader, channelId, n)) return n;
        }
        return null;
    }

    private static bool Changed(ITelemetryReader reader, Guid channelId, long sequence)
    {
        var previous = TypedChannelResponseMapper.Map(reader.Read(sequence - 1));
        var current = TypedChannelResponseMapper.Map(reader.Read(sequence));
        var hasPrevious = previous.TryGetValue(channelId, out var before);
        var hasCurrent = current.TryGetValue(channelId, out var after);
        if (hasPrevious != hasCurrent) return true;
        if (!hasPrevious) return false;
        return before!.ValueType != after!.ValueType || !Equals(before.Value, after.Value);
    }
}
