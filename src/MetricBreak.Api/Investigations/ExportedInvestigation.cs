namespace MetricBreak.Api.Investigations;

public sealed record ExportedInvestigation(
    int SchemaVersion,
    Guid InvestigationId,
    DateTimeOffset ExportedAtUtc,
    RecordingExportSnapshot Recording,
    SessionExportSnapshot Session,
    IReadOnlyList<BreakpointExportSnapshot> Breakpoints,
    IReadOnlyList<InvestigationBookmarkSnapshot>? Bookmarks = null,
    InvestigationMetadataSnapshot? Metadata = null,
    InvestigationUiStateSnapshot? UiState = null,
    RecordingTimelineSnapshot? Timeline = null);

public sealed record RecordingExportSnapshot(
    string SourceName,
    string SourceContentHash,
    long SourceSizeInBytes,
    string RecordingName,
    long SampleCount,
    Guid RecordingId = default,
    TimestampMappingSnapshot? TimestampMapping = null);

/// <summary>
/// The timestamp column and format used when this recording was imported.
/// Reopening replays that choice instead of detecting it again.
/// </summary>
public sealed record TimestampMappingSnapshot(
    int ColumnIndex,
    string Format);

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
