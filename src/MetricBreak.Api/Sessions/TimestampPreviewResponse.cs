namespace MetricBreak.Api.Sessions;

public sealed record TimestampPreviewResponse(
    IReadOnlyList<TimestampColumnResponse> Columns,
    int SuggestedColumnIndex,
    string SuggestedColumnName,
    string SuggestedFormat,
    bool Ambiguous,
    int AppliedColumnIndex,
    string AppliedFormat,
    string TimestampOrigin,
    IReadOnlyList<TimestampPreviewRowResponse> Preview,
    string? Note);

public sealed record TimestampColumnResponse(
    int Index,
    string Name);

public sealed record TimestampPreviewRowResponse(
    long SampleIndex,
    string Raw,
    string Normalized);
