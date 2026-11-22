namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Stops when data has been missing for longer than a duration.
/// With no channel, the gap is the clock jump between adjacent samples.
/// With a channel, the gap is the time since that channel last had a value,
/// and the breakpoint fires once when the run first exceeds the duration.
/// </summary>
public sealed class MissingDataBreakpoint : IBreakpoint
{
    public const string OperatorName = "MissingLongerThan";

    public Guid Id { get; }

    public double MinimumSeconds { get; }

    public long MinimumNanoseconds { get; }

    /// <summary>
    /// Null stops on a recording time gap. A channel stops when that channel stays empty.
    /// </summary>
    public Guid? ChannelId { get; }

    public MissingDataBreakpoint(
        Guid id,
        double minimumSeconds,
        Guid? channelId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(id));
        }

        if (!double.IsFinite(minimumSeconds) || minimumSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSeconds),
                "Missing-data duration must be a finite number of seconds greater than zero.");
        }

        var nanos = minimumSeconds * 1_000_000_000d;
        if (nanos >= long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSeconds),
                "Missing-data duration is too large.");
        }

        var rounded = (long)Math.Round(
            nanos,
            MidpointRounding.AwayFromZero);

        if (rounded <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSeconds),
                "Missing-data duration must be greater than zero.");
        }

        Id = id;
        MinimumSeconds = minimumSeconds;
        MinimumNanoseconds = rounded;
        ChannelId = channelId is Guid channel && channel != Guid.Empty
            ? channel
            : null;
    }
}
