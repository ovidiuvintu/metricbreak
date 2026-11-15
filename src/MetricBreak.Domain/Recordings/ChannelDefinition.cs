namespace MetricBreak.Domain.Recordings;

public sealed class ChannelDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public string? Unit { get; }
    public ChannelValueType ValueType { get; }

    public ChannelDefinition(Guid id, string name, string? unit)
        : this(id, name, unit, ChannelValueType.Numeric)
    {
    }

    public ChannelDefinition(Guid id, string name, string? unit, ChannelValueType valueType)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Channel ID cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name is required.", nameof(name));

        if (!Enum.IsDefined(valueType))
            throw new ArgumentOutOfRangeException(nameof(valueType));

        Id = id;
        Name = name;
        Unit = unit;
        ValueType = valueType;
    }
}
