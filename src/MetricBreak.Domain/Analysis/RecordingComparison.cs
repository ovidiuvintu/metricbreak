using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Analysis;

/// <summary>
/// Sample-by-sample comparison of two recordings, aligned by sequence number
/// and paired by channel name.
/// </summary>
public sealed class RecordingComparison
{
    public RecordingComparison(
        long subjectSampleCount,
        long referenceSampleCount,
        long comparedSampleCount,
        IReadOnlyList<string> onlyInSubject,
        IReadOnlyList<string> onlyInReference,
        IReadOnlyList<ChannelDivergence> channels)
    {
        SubjectSampleCount = subjectSampleCount;
        ReferenceSampleCount = referenceSampleCount;
        ComparedSampleCount = comparedSampleCount;
        OnlyInSubject = onlyInSubject;
        OnlyInReference = onlyInReference;
        Channels = channels;
    }

    public long SubjectSampleCount { get; }

    public long ReferenceSampleCount { get; }

    public long ComparedSampleCount { get; }

    public IReadOnlyList<string> OnlyInSubject { get; }

    public IReadOnlyList<string> OnlyInReference { get; }

    public IReadOnlyList<ChannelDivergence> Channels { get; }
}

public sealed class ChannelDivergence
{
    public ChannelDivergence(
        Guid channelId,
        string name,
        string? unit,
        string valueType,
        long comparedCount,
        long mismatchCount,
        long? firstMismatchSequence,
        double? maxAbsoluteDelta,
        long? maxAbsoluteDeltaSequence)
    {
        ChannelId = channelId;
        Name = name;
        Unit = unit;
        ValueType = valueType;
        ComparedCount = comparedCount;
        MismatchCount = mismatchCount;
        FirstMismatchSequence = firstMismatchSequence;
        MaxAbsoluteDelta = maxAbsoluteDelta;
        MaxAbsoluteDeltaSequence = maxAbsoluteDeltaSequence;
    }

    public Guid ChannelId { get; }

    public string Name { get; }

    public string? Unit { get; }

    public string ValueType { get; }

    public long ComparedCount { get; }

    public long MismatchCount { get; }

    public long? FirstMismatchSequence { get; }

    public double? MaxAbsoluteDelta { get; }

    public long? MaxAbsoluteDeltaSequence { get; }
}

public static class RecordingComparer
{
    public static RecordingComparison Compare(
        ITelemetryReader subject,
        IReadOnlyList<ChannelDefinition> subjectChannels,
        ITelemetryReader reference,
        IReadOnlyList<ChannelDefinition> referenceChannels)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(subjectChannels);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(referenceChannels);

        if (subject.SampleCount == 0)
            throw new ArgumentOutOfRangeException(nameof(subject), "Recording contains no samples.");

        if (reference.SampleCount == 0)
            throw new ArgumentOutOfRangeException(nameof(reference), "Known-good recording contains no samples.");

        var subjectByName = Index(subjectChannels);
        var referenceByName = Index(referenceChannels);
        var onlyInSubject = subjectByName.Keys
            .Where(name => !referenceByName.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var onlyInReference = referenceByName.Keys
            .Where(name => !subjectByName.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var shared = subjectByName.Keys
            .Where(referenceByName.ContainsKey)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new Accumulator(subjectByName[name], referenceByName[name]))
            .ToArray();

        var compared = Math.Min(subject.SampleCount, reference.SampleCount);
        for (var sequence = 0L; sequence < compared; sequence++)
        {
            var left = subject.Read(sequence);
            var right = reference.Read(sequence);
            foreach (var accumulator in shared)
                accumulator.Observe(sequence, left, right);
        }

        var channels = shared
            .Select(accumulator => accumulator.ToDivergence(compared))
            .OrderByDescending(channel => channel.MismatchCount)
            .ThenBy(channel => channel.Name, StringComparer.Ordinal)
            .ToArray();

        return new RecordingComparison(
            subject.SampleCount,
            reference.SampleCount,
            compared,
            onlyInSubject,
            onlyInReference,
            channels);
    }

    private static Dictionary<string, ChannelDefinition> Index(
        IReadOnlyList<ChannelDefinition> channels)
    {
        var map = new Dictionary<string, ChannelDefinition>(StringComparer.Ordinal);
        foreach (var channel in channels)
        {
            if (!map.ContainsKey(channel.Name))
                map.Add(channel.Name, channel);
        }

        return map;
    }

    private sealed class Accumulator
    {
        private readonly ChannelDefinition _subject;
        private readonly ChannelDefinition _reference;
        private long _mismatches;
        private long? _firstMismatch;
        private double? _maxDelta;
        private long? _maxDeltaSequence;

        public Accumulator(ChannelDefinition subject, ChannelDefinition reference)
        {
            _subject = subject;
            _reference = reference;
        }

        public void Observe(long sequence, TelemetrySample subject, TelemetrySample reference)
        {
            var left = subject.GetTypedValue(_subject.Id);
            var right = reference.GetTypedValue(_reference.Id);
            if (Same(left, right))
                return;

            _mismatches++;
            _firstMismatch ??= sequence;

            if (left is ChannelValue.Numeric subjectValue &&
                right is ChannelValue.Numeric referenceValue &&
                double.IsFinite(subjectValue.Value) &&
                double.IsFinite(referenceValue.Value))
            {
                var delta = Math.Abs(subjectValue.Value - referenceValue.Value);
                if (_maxDelta is null || delta > _maxDelta)
                {
                    _maxDelta = delta;
                    _maxDeltaSequence = sequence;
                }
            }
        }

        public ChannelDivergence ToDivergence(long comparedCount) =>
            new(
                _subject.Id,
                _subject.Name,
                _subject.Unit,
                _subject.ValueType.ToString(),
                comparedCount,
                _mismatches,
                _firstMismatch,
                _maxDelta,
                _maxDeltaSequence);

        private static bool Same(ChannelValue? left, ChannelValue? right)
        {
            if (left is null || right is null)
                return left is null && right is null;

            return (left, right) switch
            {
                (ChannelValue.Numeric a, ChannelValue.Numeric b) =>
                    a.Value.Equals(b.Value) || (double.IsNaN(a.Value) && double.IsNaN(b.Value)),
                (ChannelValue.Boolean a, ChannelValue.Boolean b) => a.Value == b.Value,
                (ChannelValue.Categorical a, ChannelValue.Categorical b) =>
                    string.Equals(a.Value, b.Value, StringComparison.Ordinal),
                _ => false,
            };
        }
    }
}
