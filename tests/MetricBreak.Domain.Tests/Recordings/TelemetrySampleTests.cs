
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class TelemetrySampleTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesSample()
    {
        var channelId = Guid.NewGuid();
        var timestamp = new TelemetryTimestamp(1_000_000_123);

        var sample = new TelemetrySample(
            42,
            timestamp,
            new Dictionary<Guid, double?>
            {
                [channelId] = 8500
            });

        Assert.Equal(42, sample.SequenceNumber);
        Assert.Equal(timestamp, sample.Timestamp);
        Assert.Equal(8500d, sample.GetValue(channelId));
        Assert.True(sample.HasChannel(channelId));
    }

    [Fact]
    public void Constructor_WithNegativeSequenceNumber_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TelemetrySample(
                -1,
                new TelemetryTimestamp(0),
                new Dictionary<Guid, double?>()));
    }

    [Fact]
    public void Constructor_WithNullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(0),
                null!));
    }

    [Fact]
    public void Constructor_WithEmptyChannelId_Throws()
    {
        var values = new Dictionary<Guid, double?>
        {
            [Guid.Empty] = 8500
        };

        Assert.Throws<ArgumentException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(0),
                values));
    }

    [Fact]
    public void Constructor_CopiesInputValues()
    {
        var channelId = Guid.NewGuid();

        var values = new Dictionary<Guid, double?>
        {
            [channelId] = 8500
        };

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(0),
            values);

        // Modify the original dictionary.
        values[channelId] = 9000;

        // The sample must remain unchanged.
        Assert.Equal(8500d, sample.GetValue(channelId));
    }

    [Fact]
    public void Values_CannotBeModifiedThroughDictionaryInterface()
    {
        var channelId = Guid.NewGuid();

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(0),
            new Dictionary<Guid, double?>
            {
                [channelId] = 8500
            });

        var dictionary =
            Assert.IsAssignableFrom<IDictionary<Guid, double?>>(
                sample.Values);

        Assert.Throws<NotSupportedException>(() =>
            dictionary[channelId] = 9000);
    }

    [Fact]
    public void GetValue_WithMissingChannel_ReturnsNull()
    {
        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(0),
            new Dictionary<Guid, double?>());

        Assert.Null(sample.GetValue(Guid.NewGuid()));
    }

    [Fact]
    public void HasChannel_DistinguishesNullValueFromAbsentChannel()
    {
        var channelId = Guid.NewGuid();

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(0),
            new Dictionary<Guid, double?>
            {
                [channelId] = null
            });

        Assert.True(sample.HasChannel(channelId));
        Assert.Null(sample.GetValue(channelId));
        Assert.False(sample.HasChannel(Guid.NewGuid()));
    }

    [Fact]
    public void Samples_WithDuplicateTimestamps_AreAllowed()
    {
        var timestamp = new TelemetryTimestamp(1_000_000_000);

        var first = new TelemetrySample(
            0,
            timestamp,
            new Dictionary<Guid, double?>());

        var second = new TelemetrySample(
            1,
            timestamp,
            new Dictionary<Guid, double?>());

        Assert.Equal(first.Timestamp, second.Timestamp);
        Assert.NotEqual(
            first.SequenceNumber,
            second.SequenceNumber);
    }
}
