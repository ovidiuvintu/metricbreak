namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Defines how conditions within a composite breakpoint
/// are logically combined.
/// </summary>
public enum LogicalOperator
{
    /// <summary>
    /// All conditions must be satisfied.
    /// </summary>
    And,

    /// <summary>
    /// At least one condition must be satisfied.
    /// </summary>
    Or
}
