namespace MetricBreak.Api.Sessions;

public sealed record ExportedInvestigation(
    int SchemaVersion,
    Guid InvestigationId,
    DateTimeOffset ExportedAtUtc,
    RecordingExportSnapshot Recording,
    SessionExportSnapshot Session,
    IReadOnlyList<BreakpointExportSnapshot> Breakpoints);

public sealed record RecordingExportSnapshot(
    string SourceName,
    string SourceContentHash,
    long SourceSizeInBytes,
    string RecordingName,
    long SampleCount);

public sealed record SessionExportSnapshot(
    long SequenceNumber,
    string ExecutionState,
    string PauseReason);

public sealed record BreakpointExportSnapshot(
    Guid BreakpointId,
    string ChannelName,
    string Operator,
    double Threshold,
    bool IsEnabled);
