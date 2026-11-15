namespace MetricBreak.Domain.Recordings;

public abstract record ChannelValue
{
    private ChannelValue() { }

    public sealed record Numeric(double Value) : ChannelValue;

    public sealed record Boolean(bool Value) : ChannelValue;

    public sealed record Categorical : ChannelValue
    {
        public string Value { get; }

        public Categorical(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }
    }
}
