using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Analysis;

/// <summary>
/// One replay sample, the changes that led into it, and how long each
/// breakpoint condition holds on the run that contains it.
/// </summary>
public sealed class EventAnalysis
{
    public EventAnalysis(
        long eventSequence,
        long eventTimestampNanoseconds,
        long lookbackStartSequence,
        long? precedingSequence,
        IReadOnlyList<ChannelSnapshot> states,
        IReadOnlyList<PrecedingChange> immediateChanges,
        IReadOnlyList<ChannelFirstMove> firstMoves,
        IReadOnlyList<ConditionRun> conditionRuns)
    {
        EventSequence = eventSequence;
        EventTimestampNanoseconds = eventTimestampNanoseconds;
        LookbackStartSequence = lookbackStartSequence;
        PrecedingSequence = precedingSequence;
        States = states;
        ImmediateChanges = immediateChanges;
        FirstMoves = firstMoves;
        ConditionRuns = conditionRuns;
    }

    public long EventSequence { get; }

    public long EventTimestampNanoseconds { get; }

    /// <summary>First sample included when ordering which channel moved first.</summary>
    public long LookbackStartSequence { get; }

    /// <summary>The sample immediately before the event. Null at the start of the recording.</summary>
    public long? PrecedingSequence { get; }

    public IReadOnlyList<ChannelSnapshot> States { get; }

    public IReadOnlyList<PrecedingChange> ImmediateChanges { get; }

    public IReadOnlyList<ChannelFirstMove> FirstMoves { get; }

    public IReadOnlyList<ConditionRun> ConditionRuns { get; }
}

public sealed record AnalysisReading(
    string ValueType,
    double? NumericValue,
    bool? BooleanValue,
    string? Text)
{
    public static AnalysisReading From(ChannelValue? value) => value switch
    {
        null => new("Missing", null, null, null),
        ChannelValue.Numeric numeric => new(
            "Numeric",
            double.IsFinite(numeric.Value) ? numeric.Value : null,
            null,
            null),
        ChannelValue.Boolean boolean => new("Boolean", null, boolean.Value, null),
        ChannelValue.Categorical categorical => new("Categorical", null, null, categorical.Value),
        _ => new("Missing", null, null, null),
    };
}

public sealed class ChannelSnapshot
{
    public ChannelSnapshot(
        Guid channelId,
        string name,
        string? unit,
        long sequence,
        AnalysisReading value)
    {
        ChannelId = channelId;
        Name = name;
        Unit = unit;
        Sequence = sequence;
        Value = value;
    }

    public Guid ChannelId { get; }

    public string Name { get; }

    public string? Unit { get; }

    public long Sequence { get; }

    public AnalysisReading Value { get; }
}

public sealed class PrecedingChange
{
    public PrecedingChange(
        Guid channelId,
        string name,
        string? unit,
        long previousSequence,
        long eventSequence,
        AnalysisReading previous,
        AnalysisReading current)
    {
        ChannelId = channelId;
        Name = name;
        Unit = unit;
        PreviousSequence = previousSequence;
        EventSequence = eventSequence;
        Previous = previous;
        Current = current;
    }

    public Guid ChannelId { get; }

    public string Name { get; }

    public string? Unit { get; }

    public long PreviousSequence { get; }

    public long EventSequence { get; }

    public AnalysisReading Previous { get; }

    public AnalysisReading Current { get; }
}

public sealed class ChannelFirstMove
{
    public ChannelFirstMove(
        Guid channelId,
        string name,
        string? unit,
        long baselineSequence,
        long firstChangeSequence,
        AnalysisReading baseline,
        AnalysisReading value)
    {
        ChannelId = channelId;
        Name = name;
        Unit = unit;
        BaselineSequence = baselineSequence;
        FirstChangeSequence = firstChangeSequence;
        Baseline = baseline;
        Value = value;
    }

    public Guid ChannelId { get; }

    public string Name { get; }

    public string? Unit { get; }

    public long BaselineSequence { get; }

    /// <summary>Earliest sample in the lookback where this channel left its starting value.</summary>
    public long FirstChangeSequence { get; }

    public AnalysisReading Baseline { get; }

    public AnalysisReading Value { get; }
}

public sealed class ConditionRun
{
    public ConditionRun(
        Guid breakpointId,
        bool isEnabled,
        bool matchesAtEvent,
        long? startSequence,
        long? endSequence,
        long? sampleCount,
        long? durationNanoseconds)
    {
        BreakpointId = breakpointId;
        IsEnabled = isEnabled;
        MatchesAtEvent = matchesAtEvent;
        StartSequence = startSequence;
        EndSequence = endSequence;
        SampleCount = sampleCount;
        DurationNanoseconds = durationNanoseconds;
    }

    public Guid BreakpointId { get; }

    public bool IsEnabled { get; }

    public bool MatchesAtEvent { get; }

    public long? StartSequence { get; }

    public long? EndSequence { get; }

    public long? SampleCount { get; }

    /// <summary>
    /// Time from the first matching sample to the last. Zero when the run is one sample.
    /// </summary>
    public long? DurationNanoseconds { get; }
}
