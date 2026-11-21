using MetricBreak.Domain.Recordings;

namespace MetricBreak.Api.Breakpoints;

internal static class BreakpointValueParser
{
    public static ChannelValue? Parse(string? value)
    {
        if (value is null) return null;
        if (bool.TryParse(value, out var boolean)) return new ChannelValue.Boolean(boolean);
        return new ChannelValue.Categorical(value);
    }

    public static string? ToWire(ChannelValue? value) => value switch
    {
        ChannelValue.Boolean b => b.Value ? "true" : "false",
        ChannelValue.Categorical c => c.Value,
        _ => null
    };
}
