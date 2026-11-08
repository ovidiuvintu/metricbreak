
namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Manages registered conditional breakpoints and their
/// enabled state while preserving registration order.
/// </summary>
public sealed class BreakpointCollection
{
    private sealed class Entry
    {
        public ConditionalBreakpoint Breakpoint { get; }

        public bool IsEnabled { get; set; }

        public Entry(ConditionalBreakpoint breakpoint)
        {
            Breakpoint = breakpoint;
            IsEnabled = true;
        }
    }

    private readonly Dictionary<Guid, Entry> _entries = new();

    private readonly List<Guid> _registrationOrder = new();

    public int Count => _entries.Count;

    public int ActiveCount =>
        _entries.Values.Count(entry => entry.IsEnabled);

    /// <summary>
    /// Registers a breakpoint, enabled by default.
    /// </summary>
    public void Add(ConditionalBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        if (!_entries.TryAdd(
                breakpoint.Id,
                new Entry(breakpoint)))
        {
            throw new ArgumentException(
                "A breakpoint with this ID is already registered.",
                nameof(breakpoint));
        }

        _registrationOrder.Add(breakpoint.Id);
    }

    /// <summary>
    /// Removes a breakpoint from the collection.
    /// Returns false if it is not registered.
    /// </summary>
    public bool Remove(Guid breakpointId)
    {
        ValidateId(breakpointId);

        if (!_entries.Remove(breakpointId))
            return false;

        _registrationOrder.Remove(breakpointId);

        return true;
    }

    /// <summary>
    /// Enables a registered breakpoint.
    /// Returns false if it does not exist.
    /// </summary>
    public bool Enable(Guid breakpointId)
    {
        ValidateId(breakpointId);

        if (!_entries.TryGetValue(
                breakpointId,
                out var entry))
        {
            return false;
        }

        entry.IsEnabled = true;

        return true;
    }

    /// <summary>
    /// Disables a registered breakpoint.
    /// Returns false if it does not exist.
    /// </summary>
    public bool Disable(Guid breakpointId)
    {
        ValidateId(breakpointId);

        if (!_entries.TryGetValue(
                breakpointId,
                out var entry))
        {
            return false;
        }

        entry.IsEnabled = false;

        return true;
    }

    /// <summary>
    /// Determines whether a registered breakpoint is enabled.
    /// Returns false for unknown breakpoint IDs.
    /// </summary>
    public bool IsEnabled(Guid breakpointId)
    {
        ValidateId(breakpointId);

        return _entries.TryGetValue(
                   breakpointId,
                   out var entry)
               && entry.IsEnabled;
    }

    /// <summary>
    /// Returns a snapshot of enabled breakpoints
    /// in registration order.
    /// </summary>
    public IReadOnlyList<ConditionalBreakpoint> GetActive()
    {
        var active = _registrationOrder
            .Select(id => _entries[id])
            .Where(entry => entry.IsEnabled)
            .Select(entry => entry.Breakpoint)
            .ToArray();

        return Array.AsReadOnly(active);
    }

    /// <summary>
    /// Removes all registered breakpoints.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _registrationOrder.Clear();
    }

    private static void ValidateId(Guid breakpointId)
    {
        if (breakpointId == Guid.Empty)
        {
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(breakpointId));
        }
    }
}
