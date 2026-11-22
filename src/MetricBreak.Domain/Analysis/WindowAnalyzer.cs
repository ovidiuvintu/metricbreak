using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Analysis;

/// <summary>
/// One pass over a sample window. Missing measurements stay out of numeric
/// averages, Boolean counts, and correlations.
/// </summary>
public static class WindowAnalyzer
{
    public static WindowAnalysis Analyze(
        ITelemetryReader reader,
        IReadOnlyList<ChannelDefinition> channels,
        long startSequence,
        long endSequenceInclusive)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Count == 0)
            throw new ArgumentException("At least one channel is required.", nameof(channels));

        if (reader.SampleCount == 0)
            throw new ArgumentOutOfRangeException(nameof(reader), "Recording contains no samples.");

        if (startSequence < 0 || startSequence >= reader.SampleCount)
            throw new ArgumentOutOfRangeException(nameof(startSequence));

        if (endSequenceInclusive < startSequence || endSequenceInclusive >= reader.SampleCount)
            throw new ArgumentOutOfRangeException(nameof(endSequenceInclusive));

        var accumulators = new ChannelAccumulator[channels.Count];
        var numericSlots = new List<int>();
        for (var index = 0; index < channels.Count; index++)
        {
            accumulators[index] = new ChannelAccumulator(channels[index]);
            if (channels[index].ValueType == ChannelValueType.Numeric)
                numericSlots.Add(index);
        }

        var numericCount = numericSlots.Count;
        var pairs = new PairAccumulator[numericCount * (numericCount - 1) / 2];
        for (var index = 0; index < pairs.Length; index++)
            pairs[index] = new PairAccumulator();

        var numericValues = new double?[numericCount];
        long startTimestamp = 0;
        long endTimestamp = 0;

        for (var sequence = startSequence; sequence <= endSequenceInclusive; sequence++)
        {
            var sample = reader.Read(sequence);
            if (sequence == startSequence)
                startTimestamp = sample.Timestamp.UnixNanoseconds;
            if (sequence == endSequenceInclusive)
                endTimestamp = sample.Timestamp.UnixNanoseconds;

            for (var index = 0; index < accumulators.Length; index++)
            {
                accumulators[index].Add(
                    sequence,
                    sample.GetTypedValue(accumulators[index].Channel.Id));
            }

            for (var slot = 0; slot < numericCount; slot++)
            {
                var value = sample.GetTypedValue(accumulators[numericSlots[slot]].Channel.Id);
                numericValues[slot] = value is ChannelValue.Numeric numeric &&
                    double.IsFinite(numeric.Value)
                    ? numeric.Value
                    : null;
            }

            var pairIndex = 0;
            for (var left = 0; left < numericCount; left++)
            {
                if (numericValues[left] is not double leftValue)
                {
                    pairIndex += numericCount - left - 1;
                    continue;
                }

                for (var right = left + 1; right < numericCount; right++)
                {
                    if (numericValues[right] is double rightValue)
                        pairs[pairIndex].Add(leftValue, rightValue);
                    pairIndex++;
                }
            }
        }

        var summaries = new ChannelWindowSummary[accumulators.Length];
        for (var index = 0; index < accumulators.Length; index++)
            summaries[index] = accumulators[index].ToSummary();

        var correlations = new List<ChannelCorrelation>(pairs.Length);
        var pairCursor = 0;
        for (var left = 0; left < numericCount; left++)
        {
            for (var right = left + 1; right < numericCount; right++)
            {
                var leftChannel = accumulators[numericSlots[left]].Channel;
                var rightChannel = accumulators[numericSlots[right]].Channel;
                var pair = pairs[pairCursor++];
                correlations.Add(new ChannelCorrelation(
                    leftChannel.Id,
                    leftChannel.Name,
                    rightChannel.Id,
                    rightChannel.Name,
                    pair.Count,
                    pair.Correlation()));
            }
        }

        correlations.Sort(static (left, right) =>
        {
            var leftMagnitude = left.Correlation is double leftValue ? Math.Abs(leftValue) : -1;
            var rightMagnitude = right.Correlation is double rightValue ? Math.Abs(rightValue) : -1;
            var byStrength = rightMagnitude.CompareTo(leftMagnitude);
            if (byStrength != 0)
                return byStrength;

            var byName = string.Compare(left.LeftChannelName, right.LeftChannelName, StringComparison.Ordinal);
            return byName != 0
                ? byName
                : string.Compare(left.RightChannelName, right.RightChannelName, StringComparison.Ordinal);
        });

        return new WindowAnalysis(
            startSequence,
            endSequenceInclusive,
            startTimestamp,
            endTimestamp,
            endSequenceInclusive - startSequence + 1,
            summaries,
            correlations);
    }

    private sealed class ChannelAccumulator
    {
        private double _minimum;
        private double _maximum;
        private double _sum;
        private long _minimumSequence;
        private long _maximumSequence;
        private long _trueCount;
        private long _falseCount;
        private bool? _previousBoolean;
        private string? _previousCategory;
        private readonly Dictionary<string, long> _categories = new(StringComparer.Ordinal);
        private readonly List<string> _categoryOrder = [];

        public ChannelAccumulator(ChannelDefinition channel)
        {
            Channel = channel;
        }

        public ChannelDefinition Channel { get; }

        public long PresentCount { get; private set; }

        public long MissingCount { get; private set; }

        public long ChangeCount { get; private set; }

        public void Add(long sequence, ChannelValue? value)
        {
            if (!IsPresent(Channel.ValueType, value, out var numeric, out var boolean, out var category))
            {
                MissingCount++;
                return;
            }

            PresentCount++;

            if (Channel.ValueType == ChannelValueType.Numeric && numeric is double number)
            {
                _sum += number;
                if (PresentCount == 1 || number < _minimum)
                {
                    _minimum = number;
                    _minimumSequence = sequence;
                }

                if (PresentCount == 1 || number > _maximum)
                {
                    _maximum = number;
                    _maximumSequence = sequence;
                }

                return;
            }

            if (Channel.ValueType == ChannelValueType.Boolean && boolean is bool flag)
            {
                if (flag)
                    _trueCount++;
                else
                    _falseCount++;

                if (_previousBoolean is bool previous && previous != flag)
                    ChangeCount++;
                _previousBoolean = flag;
                return;
            }

            if (category is null)
                return;

            if (_previousCategory is not null && !string.Equals(_previousCategory, category, StringComparison.Ordinal))
                ChangeCount++;
            _previousCategory = category;

            if (_categories.TryGetValue(category, out var count))
            {
                _categories[category] = count + 1;
            }
            else
            {
                _categories[category] = 1;
                _categoryOrder.Add(category);
            }
        }

        public ChannelWindowSummary ToSummary()
        {
            double? minimum = null;
            long? minimumSequence = null;
            double? maximum = null;
            long? maximumSequence = null;
            double? mean = null;
            long? trueCount = null;
            long? falseCount = null;
            long? changeCount = null;
            int? distinctCount = null;
            string? mode = null;
            long? modeCount = null;

            if (Channel.ValueType == ChannelValueType.Numeric && PresentCount > 0)
            {
                minimum = _minimum;
                minimumSequence = _minimumSequence;
                maximum = _maximum;
                maximumSequence = _maximumSequence;
                mean = _sum / PresentCount;
            }
            else if (Channel.ValueType == ChannelValueType.Boolean)
            {
                trueCount = _trueCount;
                falseCount = _falseCount;
                changeCount = ChangeCount;
            }
            else if (Channel.ValueType == ChannelValueType.Categorical)
            {
                distinctCount = _categories.Count;
                changeCount = ChangeCount;
                foreach (var category in _categoryOrder)
                {
                    var count = _categories[category];
                    if (modeCount is null || count > modeCount)
                    {
                        mode = category;
                        modeCount = count;
                    }
                }
            }

            return new ChannelWindowSummary(
                Channel.Id,
                Channel.Name,
                Channel.Unit,
                Channel.ValueType.ToString(),
                PresentCount,
                MissingCount,
                changeCount,
                minimum,
                minimumSequence,
                maximum,
                maximumSequence,
                mean,
                trueCount,
                falseCount,
                distinctCount,
                mode,
                modeCount);
        }

        private static bool IsPresent(
            ChannelValueType valueType,
            ChannelValue? value,
            out double? numeric,
            out bool? boolean,
            out string? category)
        {
            numeric = null;
            boolean = null;
            category = null;

            switch (value)
            {
                case ChannelValue.Numeric number when valueType == ChannelValueType.Numeric &&
                    double.IsFinite(number.Value):
                    numeric = number.Value;
                    return true;
                case ChannelValue.Boolean flag when valueType == ChannelValueType.Boolean:
                    boolean = flag.Value;
                    return true;
                case ChannelValue.Categorical text when valueType == ChannelValueType.Categorical &&
                    text.Value.Length > 0:
                    category = text.Value;
                    return true;
                default:
                    return false;
            }
        }
    }

    private sealed class PairAccumulator
    {
        private double _sumX;
        private double _sumY;
        private double _sumXX;
        private double _sumYY;
        private double _sumXY;

        public long Count { get; private set; }

        public void Add(double left, double right)
        {
            Count++;
            _sumX += left;
            _sumY += right;
            _sumXX += left * left;
            _sumYY += right * right;
            _sumXY += left * right;
        }

        public double? Correlation()
        {
            if (Count < 2)
                return null;

            var count = (double)Count;
            var numerator = count * _sumXY - _sumX * _sumY;
            var leftSpread = count * _sumXX - _sumX * _sumX;
            var rightSpread = count * _sumYY - _sumY * _sumY;
            if (leftSpread <= 0 || rightSpread <= 0)
                return null;

            var denominator = Math.Sqrt(leftSpread * rightSpread);
            if (denominator == 0 || double.IsNaN(denominator))
                return null;

            var correlation = numerator / denominator;
            if (correlation > 1)
                return 1;
            if (correlation < -1)
                return -1;
            return correlation;
        }
    }
}
