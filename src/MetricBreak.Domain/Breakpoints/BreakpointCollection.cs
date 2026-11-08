
namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Manages breakpoint definitions and their enabled state.
/// Preserves breakpoint registration order.
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

    public void Add(ConditionalBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        if (!_entries.TryAdd(breakpoint.Id, new Entry(breakpoint)))
        {
            throw new ArgumentException(
                "A breakpoint with this ID is already registered.",
                nameof(breakpoint));
        }
    }

    public bool Remove(Guid breakpointId)
    {
        ValidateId(breakpointId);
        return _entries.Remove(breakpointId);
    }

    public bool Enable(Guid breakpointId)
    {
        ValidateId(breakpointId);

        if (!_entries.TryGetValue(breakpointId, out var entry))
            return false;

        entry.IsEnabled = true;
        return true;
    }

    public bool Disable(Guid breakpointId)
    {
        ValidateId(breakpointId);

        if (!_entries.TryGetValue(breakpointId, out var entry))
            return false;

        entry.IsEnabled = false;
        return true;
    }

    public bool IsEnabled(Guid breakpointId)
    {
        ValidateId(breakpointId);

        return _entries.TryGetValue(breakpointId, out var entry)
            && entry.IsEnabled;
    }

    public IReadOnlyList<ConditionalBreakpoint> GetActive()
    {
        return Array.AsReadOnly(
            _entries.Values
                .Where(entry => entry.IsEnabled)
                .Select(entry => entry.Breakpoint)
                .ToArray());
    }

    public void Clear() => _entries.Clear();

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
