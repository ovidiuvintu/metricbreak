namespace MetricBreak.Api.Investigations;

public sealed record InvestigationBookmarkSnapshot(
    Guid BookmarkId,
    long SampleSequence,
    long TimestampNanoseconds,
    string? Title,
    string? Annotation,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ModifiedAtUtc);