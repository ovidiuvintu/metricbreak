using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// An immutable record of a breakpoint matching a sample.
/// A hit represents one registered breakpoint,
/// regardless of the number of conditions it contains.
/// </summary>
public sealed record BreakpointHit
{
    public Guid BreakpointId { get; }

    public SamplePosition Position { get; }

    public TelemetryTimestamp Timestamp { get; }

    // Existing single-condition properties.
    public Guid ChannelId { get; }

    public double ObservedValue { get; }

    public ChannelValue? TypedObservedValue { get; init; }

    public ChannelValue? ExpectedValue { get; init; }

    public ComparisonOperator Operator { get; }

    public double Threshold { get; }

    // New composite properties.
    public bool IsComposite { get; }

    public bool IsMissingData { get; }

    public bool IsTimeline { get; }

    public string? MarkerName { get; }

    public TimelineMarkerKind? MarkerKind { get; }

    public LogicalOperator? LogicalOperator { get; }

    public IReadOnlyList<BreakpointConditionHit> Conditions { get; }

    /// <summary>
    /// Existing constructor, retained for compatibility.
    /// </summary>
    public BreakpointHit(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        Guid channelId,
        double observedValue,
        ComparisonOperator comparisonOperator,
        double threshold)
    {
        ValidateCommon(breakpointId, position);

        if (channelId == Guid.Empty)
            throw new ArgumentException(
                "Channel ID cannot be empty.",
                nameof(channelId));

        if (!Enum.IsDefined(comparisonOperator))
            throw new ArgumentOutOfRangeException(
                nameof(comparisonOperator));

        if (double.IsNaN(observedValue))
            throw new ArgumentOutOfRangeException(
                nameof(observedValue));

        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(
                nameof(threshold));

        BreakpointId = breakpointId;
        Position = position;
        Timestamp = timestamp;

        ChannelId = channelId;
        ObservedValue = observedValue;
        Operator = comparisonOperator;
        Threshold = threshold;

        IsComposite = false;
        IsMissingData = false;
        LogicalOperator = null;

        Conditions = Array.AsReadOnly(
            new[]
            {
                new BreakpointConditionHit(
                    channelId,
                    observedValue,
                    comparisonOperator,
                    threshold,
                    Matched: true)
            });
    }

    public static BreakpointHit ForMissingData(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        Guid channelId,
        double gapSeconds,
        double minimumSeconds)
    {
        return new BreakpointHit(
            breakpointId,
            position,
            timestamp,
            channelId,
            gapSeconds,
            minimumSeconds,
            missingData: true);
    }

    private BreakpointHit(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        Guid channelId,
        double gapSeconds,
        double minimumSeconds,
        bool missingData)
    {
        if (!missingData)
        {
            throw new ArgumentException(
                "Use the comparison constructor for a value breakpoint.",
                nameof(missingData));
        }

        ValidateCommon(breakpointId, position);

        if (double.IsNaN(gapSeconds) || double.IsInfinity(gapSeconds) || gapSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gapSeconds));
        }

        if (!double.IsFinite(minimumSeconds) || minimumSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSeconds));
        }

        BreakpointId = breakpointId;
        Position = position;
        Timestamp = timestamp;
        ChannelId = channelId;
        ObservedValue = gapSeconds;
        Operator = default;
        Threshold = minimumSeconds;
        IsComposite = false;
        IsMissingData = true;
        LogicalOperator = null;
        Conditions = Array.AsReadOnly(Array.Empty<BreakpointConditionHit>());
    }

    public static BreakpointHit ForTimeline(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        TimelineMarkerKind kind,
        string markerName)
    {
        return new BreakpointHit(
            breakpointId,
            position,
            timestamp,
            kind,
            markerName,
            timeline: true);
    }

    private BreakpointHit(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        TimelineMarkerKind kind,
        string markerName,
        bool timeline)
    {
        if (!timeline)
        {
            throw new ArgumentException(
                "Use the comparison constructor for a value breakpoint.",
                nameof(timeline));
        }

        ValidateCommon(breakpointId, position);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(markerName))
        {
            throw new ArgumentException(
                "Marker name cannot be empty.",
                nameof(markerName));
        }

        BreakpointId = breakpointId;
        Position = position;
        Timestamp = timestamp;
        ChannelId = Guid.Empty;
        ObservedValue = 0;
        Operator = default;
        Threshold = 0;
        IsComposite = false;
        IsMissingData = false;
        IsTimeline = true;
        MarkerKind = kind;
        MarkerName = markerName.Trim();
        LogicalOperator = null;
        Conditions = Array.AsReadOnly(Array.Empty<BreakpointConditionHit>());
    }

    /// <summary>
    /// Constructor for a composite breakpoint hit.
    /// </summary>
    public BreakpointHit(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        LogicalOperator logicalOperator,
        IEnumerable<BreakpointConditionHit> conditions)
    {
        ValidateCommon(breakpointId, position);

        if (!Enum.IsDefined(logicalOperator))
            throw new ArgumentOutOfRangeException(
                nameof(logicalOperator));

        ArgumentNullException.ThrowIfNull(conditions);

        var conditionArray = conditions.ToArray();

        if (conditionArray.Length < 2)
        {
            throw new ArgumentException(
                "Composite hits require at least two conditions.",
                nameof(conditions));
        }

        if (conditionArray.Any(c => c is null))
        {
            throw new ArgumentException(
                "Conditions cannot contain null entries.",
                nameof(conditions));
        }

        foreach (var condition in conditionArray)
        {
            if (condition.ChannelId == Guid.Empty ||
                !Enum.IsDefined(condition.Operator) ||
                !double.IsFinite(condition.Threshold) ||
                condition.ObservedValue is double value &&
                !double.IsFinite(value))
            {
                throw new ArgumentException(
                    "Composite hit contains an invalid condition.",
                    nameof(conditions));
            }
        }

        BreakpointId = breakpointId;
        Position = position;
        Timestamp = timestamp;

        IsComposite = true;
        IsMissingData = false;
        LogicalOperator = logicalOperator;
        Conditions = Array.AsReadOnly(conditionArray);

        // These legacy properties are not meaningful
        // for a multi-condition breakpoint.
        ChannelId = Guid.Empty;
        ObservedValue = double.NaN;
        Operator = default;
        Threshold = double.NaN;
    }

    public bool Equals(BreakpointHit? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return BreakpointId == other.BreakpointId
            && Position == other.Position
            && Timestamp == other.Timestamp
            && ChannelId == other.ChannelId
            && ObservedValue.Equals(other.ObservedValue)
            && Operator == other.Operator
            && Threshold.Equals(other.Threshold)
            && IsComposite == other.IsComposite
            && IsMissingData == other.IsMissingData
            && IsTimeline == other.IsTimeline
            && MarkerName == other.MarkerName
            && MarkerKind == other.MarkerKind
            && LogicalOperator == other.LogicalOperator
            && Conditions.SequenceEqual(other.Conditions);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(BreakpointId);
        hash.Add(Position);
        hash.Add(Timestamp);
        hash.Add(ChannelId);
        hash.Add(ObservedValue);
        hash.Add(Operator);
        hash.Add(Threshold);
        hash.Add(IsComposite);
        hash.Add(IsMissingData);
        hash.Add(IsTimeline);
        hash.Add(MarkerName);
        hash.Add(MarkerKind);
        hash.Add(LogicalOperator);

        foreach (var condition in Conditions)
        {
            hash.Add(condition);
        }

        return hash.ToHashCode();
    }

    private static void ValidateCommon(
        Guid breakpointId,
        SamplePosition position)
    {
        if (breakpointId == Guid.Empty)
        {
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(breakpointId));
        }

        if (position.RecordingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sample position must identify a recording.",
                nameof(position));
        }

        if (position.SequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position));
        }
    }
}
