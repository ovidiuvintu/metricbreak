
namespace MetricBreak.Domain.Recordings;
public sealed class ChannelDefinition
{
    public Guid Id { get; }

    public string Name { get; }

    public string? Unit { get; }

    public ChannelDefinition(
        Guid id,
        string name,
        string? unit)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Channel ID cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Channel name is required.", nameof(name));

        Id = id;
        Name = name;
        Unit = unit;
    }
}
