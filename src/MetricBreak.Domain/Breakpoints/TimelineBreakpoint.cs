namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Stops on annotated timeline samples. A stage stops at the stage start,
/// not on every sample while that stage is active.
/// A null name matches every marker of the kind.
/// </summary>
public sealed class TimelineBreakpoint : IBreakpoint
{
    public const string EventOperatorName = "TimelineEvent";

    public const string StageOperatorName = "TimelineStage";

    private readonly Dictionary<long, string> _markers;

    public Guid Id { get; }

    public TimelineMarkerKind Kind { get; }

    /// <summary>
    /// Marker name to match, or null to match every marker of this kind.
    /// </summary>
    public string? Name { get; }

    public string OperatorName =>
        Kind == TimelineMarkerKind.Stage ? StageOperatorName : EventOperatorName;

    public TimelineBreakpoint(
        Guid id,
        TimelineMarkerKind kind,
        string? name,
        IReadOnlyDictionary<long, string> markers)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(markers);

        if (markers.Count == 0)
        {
            throw new ArgumentException(
                "A timeline breakpoint needs at least one marker.",
                nameof(markers));
        }

        var copy = new Dictionary<long, string>(markers.Count);
        foreach (var (sequence, markerName) in markers)
        {
            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(markers),
                    "Marker sample sequences cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(markerName))
            {
                throw new ArgumentException(
                    "Marker names cannot be empty.",
                    nameof(markers));
            }

            copy[sequence] = markerName.Trim();
        }

        Id = id;
        Kind = kind;
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        _markers = copy;
    }

    public bool Matches(long sequence) => _markers.ContainsKey(sequence);

    public bool TryGetMarkerName(long sequence, out string name) =>
        _markers.TryGetValue(sequence, out name!);

    public IReadOnlyCollection<long> SampleSequences => _markers.Keys;
}
