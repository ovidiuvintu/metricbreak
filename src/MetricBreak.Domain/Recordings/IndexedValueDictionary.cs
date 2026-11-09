
using System.Collections;

namespace MetricBreak.Domain.Recordings;

internal sealed class IndexedValueDictionary
    : IReadOnlyDictionary<Guid, double?>
{
    private readonly ChannelIndex _index;
    private readonly double?[] _values;

    public int Count => _values.Length;

    public IEnumerable<Guid> Keys => _index.ChannelIds;

    public IEnumerable<double?> Values => _values;

    public double? this[Guid key]
    {
        get
        {
            var position = _index.GetPosition(key);
            return _values[position];
        }
    }

    public IndexedValueDictionary(
        ChannelIndex index,
        double?[] values)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(values);

        if (index.Count != values.Length)
        {
            throw new ArgumentException(
                "Channel and value counts must match.",
                nameof(values));
        }

        _index = index;
        _values = values;
    }

    public bool ContainsKey(Guid key)
    {
        return _index.Contains(key);
    }

    public bool TryGetValue(
        Guid key,
        out double? value)
    {
        if (_index.TryGetPosition(key, out var position))
        {
            value = _values[position];
            return true;
        }

        value = null;
        return false;
    }

    public IEnumerator<KeyValuePair<Guid, double?>> GetEnumerator()
    {
        for (var position = 0; position < _values.Length; position++)
        {
            yield return new KeyValuePair<Guid, double?>(
                _index.ChannelIds[position],
                _values[position]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
