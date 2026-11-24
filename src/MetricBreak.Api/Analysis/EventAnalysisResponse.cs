using MetricBreak.Domain.Analysis;

namespace MetricBreak.Api.Analysis;

public sealed record EventAnalysisResponse(
    long EventSequence,
    string EventTimestampNanoseconds,
    long LookbackStartSequence,
    long? PrecedingSequence,
    IReadOnlyList<ChannelSnapshotResponse> States,
    IReadOnlyList<PrecedingChangeResponse> ImmediateChanges,
    IReadOnlyList<ChannelFirstMoveResponse> FirstMoves,
    IReadOnlyList<ConditionRunResponse> ConditionRuns)
{
    public static EventAnalysisResponse From(
        EventAnalysis analysis,
        IReadOnlyDictionary<Guid, string> breakpointLabels)
    {
        return new EventAnalysisResponse(
            analysis.EventSequence,
            Nanoseconds(analysis.EventTimestampNanoseconds),
            analysis.LookbackStartSequence,
            analysis.PrecedingSequence,
            analysis.States
                .Select(state => new ChannelSnapshotResponse(
                    state.ChannelId,
                    state.Name,
                    state.Unit,
                    state.Sequence,
                    AnalysisReadingResponse.From(state.Value)))
                .ToArray(),
            analysis.ImmediateChanges
                .Select(change => new PrecedingChangeResponse(
                    change.ChannelId,
                    change.Name,
                    change.Unit,
                    change.PreviousSequence,
                    change.EventSequence,
                    AnalysisReadingResponse.From(change.Previous),
                    AnalysisReadingResponse.From(change.Current)))
                .ToArray(),
            analysis.FirstMoves
                .Select(move => new ChannelFirstMoveResponse(
                    move.ChannelId,
                    move.Name,
                    move.Unit,
                    move.BaselineSequence,
                    move.FirstChangeSequence,
                    AnalysisReadingResponse.From(move.Baseline),
                    AnalysisReadingResponse.From(move.Value)))
                .ToArray(),
            analysis.ConditionRuns
                .Select(run => new ConditionRunResponse(
                    run.BreakpointId,
                    breakpointLabels.TryGetValue(run.BreakpointId, out var label)
                        ? label
                        : run.BreakpointId.ToString(),
                    run.IsEnabled,
                    run.MatchesAtEvent,
                    run.StartSequence,
                    run.EndSequence,
                    run.SampleCount,
                    run.DurationNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ToArray());
    }

    private static string Nanoseconds(long value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record AnalysisReadingResponse(
    string ValueType,
    double? NumericValue,
    bool? BooleanValue,
    string? Text)
{
    public static AnalysisReadingResponse From(AnalysisReading reading) =>
        new(reading.ValueType, reading.NumericValue, reading.BooleanValue, reading.Text);
}

public sealed record ChannelSnapshotResponse(
    Guid ChannelId,
    string Name,
    string? Unit,
    long Sequence,
    AnalysisReadingResponse Value);

public sealed record PrecedingChangeResponse(
    Guid ChannelId,
    string Name,
    string? Unit,
    long PreviousSequence,
    long EventSequence,
    AnalysisReadingResponse Previous,
    AnalysisReadingResponse Current);

public sealed record ChannelFirstMoveResponse(
    Guid ChannelId,
    string Name,
    string? Unit,
    long BaselineSequence,
    long FirstChangeSequence,
    AnalysisReadingResponse Baseline,
    AnalysisReadingResponse Value);

public sealed record ConditionRunResponse(
    Guid BreakpointId,
    string Label,
    bool IsEnabled,
    bool MatchesAtEvent,
    long? StartSequence,
    long? EndSequence,
    long? SampleCount,
    string? DurationNanoseconds);
