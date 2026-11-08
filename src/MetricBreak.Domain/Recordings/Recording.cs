
namespace MetricBreak.Domain.Recordings;

public sealed class Recording
{
    public Guid Id { get; }

    public string Name { get; }

    public RecordingSource Source { get; }

    public DateTimeOffset CreatedAt { get; }

    public Recording(
        Guid id,
        string name,
        RecordingSource source,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Recording ID cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Recording name is required.", nameof(name));

        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Name))
            throw new ArgumentException(
                "Source name is required.", nameof(source));

        if (string.IsNullOrWhiteSpace(source.ContentHash))
            throw new ArgumentException(
                "Content hash is required.", nameof(source));

        if (source.SizeInBytes < 0)
            throw new ArgumentOutOfRangeException(
                nameof(source), "Source size cannot be negative.");

        Id = id;
        Name = name;
        Source = source;
        CreatedAt = createdAt;
    }
}

