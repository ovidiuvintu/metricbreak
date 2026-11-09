
using System.Collections.ObjectModel;

namespace MetricBreak.Domain.Recordings;

public sealed class ChannelIndex
{
    private readonly Dictionary<Guid, int> _positions;
    private readonly ReadOnlyCollection<Guid> _channelIds;

    public int Count => _channelIds.Count;

    public IReadOnlyList<Guid> ChannelIds => _channelIds;

    public ChannelIndex(IEnumerable<Guid> channelIds)
    {
        ArgumentNullException.ThrowIfNull(channelIds);

        _positions = new Dictionary<Guid, int>();

        var orderedIds = new List<Guid>();

        foreach (var channelId in channelIds)
        {
            if (channelId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Channel IDs cannot be empty.",
                    nameof(channelIds));
            }

            if (!_positions.TryAdd(
                channelId,
                orderedIds.Count))
            {
                throw new ArgumentException(
                    $"Duplicate channel ID: {channelId}",
                    nameof(channelIds));
            }

            orderedIds.Add(channelId);
        }

        _channelIds = orderedIds.AsReadOnly();
    }

    public int GetPosition(Guid channelId)
    {
        if (!_positions.TryGetValue(
            channelId,
            out var position))
        {
            throw new KeyNotFoundException(
                $"Channel {channelId} does not exist.");
        }

        return position;
    }

    public bool TryGetPosition(
        Guid channelId,
        out int position)
    {
        return _positions.TryGetValue(
            channelId,
            out position);
    }

    public bool Contains(Guid channelId)
    {
        return _positions.ContainsKey(channelId);
    }
}
