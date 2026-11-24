namespace MetricBreak.Api.Investigations;

public static class InvestigationSchema
{
    public const int CurrentVersion = 3;
    public const int LegacyVersion = 1;
}

public sealed record InvestigationMetadataSnapshot(
    string? Name,
    string? Description,
    Guid? ProjectId,
    DateTimeOffset LastOpenedAtUtc);

public sealed record InvestigationChartViewportSnapshot(
    long PageStart,
    double DataZoomStartPercent,
    double DataZoomEndPercent);

public sealed record InvestigationUiStateSnapshot(
    IReadOnlyList<string> SelectedChannelIds,
    IReadOnlyList<string> RightAxisChannelIds,
    InvestigationChartViewportSnapshot? ChartViewport,
    IReadOnlyList<string>? SelectedChannelNames = null,
    IReadOnlyList<string>? RightAxisChannelNames = null);