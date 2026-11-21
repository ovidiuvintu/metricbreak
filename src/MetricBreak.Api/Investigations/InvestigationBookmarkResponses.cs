namespace MetricBreak.Api.Investigations;

public sealed record InvestigationBookmarksResponse(
    IReadOnlyList<InvestigationBookmarkItemResponse> Bookmarks);

public sealed record InvestigationBookmarkItemResponse(
    Guid BookmarkId,
    Guid InvestigationId,
    long SampleSequence,
    long TimestampNanoseconds,
    string? Title,
    string? Annotation,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ModifiedAtUtc);

public sealed record CreateInvestigationBookmarkRequest(
    long SampleSequence,
    long TimestampNanoseconds,
    string? Title,
    string? Annotation);

public sealed record UpdateInvestigationBookmarkRequest(
    string? Title,
    string? Annotation);