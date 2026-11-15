namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Represents a breakpoint containing multiple conditions
/// combined using a single logical operator.
///
/// All conditions are evaluated against the same telemetry sample.
/// Nested composite breakpoints are not supported.
/// </summary>
public sealed class CompositeBreakpoint : IBreakpoint
{
    public Guid Id { get; }

    public LogicalOperator LogicalOperator { get; }

    public IReadOnlyList<BreakpointCondition> Conditions { get; }

    public CompositeBreakpoint(
        Guid id,
        LogicalOperator logicalOperator,
        IEnumerable<BreakpointCondition> conditions)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(id));
        }

        if (!Enum.IsDefined(logicalOperator))
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalOperator));
        }

        ArgumentNullException.ThrowIfNull(conditions);

        var conditionList = conditions.ToArray();

        if (conditionList.Length < 2)
        {
            throw new ArgumentException(
                "Composite breakpoints require at least two conditions.",
                nameof(conditions));
        }

        if (conditionList.Any(condition => condition is null))
        {
            throw new ArgumentException(
                "Conditions cannot contain null entries.",
                nameof(conditions));
        }

        Id = id;
        LogicalOperator = logicalOperator;
        Conditions = Array.AsReadOnly(conditionList);
    }
}
