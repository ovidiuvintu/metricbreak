namespace MetricBreak.Api.Sessions;

public sealed record TypedChannelValueResponse(string ValueType, object? Value);
