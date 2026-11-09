
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class IndexedTelemetrySampleTests
{
    [Fact]
    public void Constructor_IndexedValues_CanBeReadByChannelId()
    {
        var pressureId = Guid.NewGuid();
        var rateId = Guid.NewGuid();

        var ids = new[] { pressureId, rateId };
        var index = new ChannelIndex(ids);

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            index,
            ids,
            [8500.0, 42.5]);

        Assert.Equal(8500.0, sample.GetValue(pressureId));
        Assert.Equal(42.5, sample.GetValue(rateId));
    }

    [Fact]
    public void Constructor_IndexedValues_PreservesNullValues()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            new ChannelIndex(ids),
            ids,
            [null]);

        Assert.True(sample.HasChannel(channelId));
        Assert.Null(sample.GetValue(channelId));
    }

    [Fact]
    public void Constructor_IndexedValues_CopiesInputArray()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };
        double?[] values = [100.0];

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            new ChannelIndex(ids),
            ids,
            values);

        values[0] = 999.0;

        Assert.Equal(100.0, sample.GetValue(channelId));
    }

    [Fact]
    public void Constructor_IndexedValues_UnknownChannelReturnsNull()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            new ChannelIndex(ids),
            ids,
            [100.0]);

        var unknownId = Guid.NewGuid();

        Assert.False(sample.HasChannel(unknownId));
        Assert.Null(sample.GetValue(unknownId));
    }

    [Fact]
    public void Values_ExposesReadOnlyDictionaryView()
    {
        var pressureId = Guid.NewGuid();
        var rateId = Guid.NewGuid();

        var ids = new[] { pressureId, rateId };

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            new ChannelIndex(ids),
            ids,
            [8500.0, 42.5]);

        Assert.Equal(2, sample.Values.Count);
        Assert.Equal(8500.0, sample.Values[pressureId]);
        Assert.Equal(42.5, sample.Values[rateId]);
        Assert.True(sample.Values.ContainsKey(pressureId));
    }

    [Fact]
    public void Values_EnumeratesChannelsInIndexOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var ids = new[] { first, second };

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            new ChannelIndex(ids),
            ids,
            [10.0, 20.0]);

        var entries = sample.Values.ToArray();

        Assert.Equal(first, entries[0].Key);
        Assert.Equal(10.0, entries[0].Value);

        Assert.Equal(second, entries[1].Key);
        Assert.Equal(20.0, entries[1].Value);
    }

    [Fact]
    public void Constructor_MismatchedChannelCount_Throws()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        Assert.Throws<ArgumentException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                new ChannelIndex(ids),
                ids,
                [10.0, 20.0]));
    }

    [Fact]
    public void Constructor_ChannelOrderMismatch_Throws()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var ids = new[] { first, second };
        var reversed = new[] { second, first };

        Assert.Throws<ArgumentException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                new ChannelIndex(ids),
                reversed,
                [10.0, 20.0]));
    }

    [Fact]
    public void Constructor_NegativeSequence_Throws()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TelemetrySample(
                -1,
                new TelemetryTimestamp(100),
                new ChannelIndex(ids),
                ids,
                [10.0]));
    }

    [Fact]
    public void Constructor_NullIndex_Throws()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        Assert.Throws<ArgumentNullException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                null!,
                ids,
                [10.0]));
    }

    [Fact]
    public void Constructor_NullValues_Throws()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        Assert.Throws<ArgumentNullException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                new ChannelIndex(ids),
                ids,
                null!));
    }

    [Fact]
    public void Values_UnknownKey_ThrowsKeyNotFoundException()
    {
        var channelId = Guid.NewGuid();
        var ids = new[] { channelId };

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            new ChannelIndex(ids),
            ids,
            [100.0]);

        Assert.Throws<KeyNotFoundException>(() =>
        {
            _ = sample.Values[Guid.NewGuid()];
        });
    }
}

