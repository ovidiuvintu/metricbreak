
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class SharedChannelIndexTests
{
    [Fact]
    public void ChannelIds_PreservesOriginalOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var index = new ChannelIndex([first, second]);

        Assert.Equal(
            new[] { first, second },
            index.ChannelIds);
    }

    [Fact]
    public void ChannelIds_CannotBeModified()
    {
        var first = Guid.NewGuid();
        var index = new ChannelIndex([first]);

        var list = Assert.IsAssignableFrom<IList<Guid>>(
            index.ChannelIds);

        Assert.Throws<NotSupportedException>(
            () => list[0] = Guid.NewGuid());
    }

    [Fact]
    public void ChannelIds_IsIndependentOfInputCollection()
    {
        var first = Guid.NewGuid();
        var input = new List<Guid> { first };

        var index = new ChannelIndex(input);

        input[0] = Guid.NewGuid();
        input.Add(Guid.NewGuid());

        Assert.Single(index.ChannelIds);
        Assert.Equal(first, index.ChannelIds[0]);
    }

    [Fact]
    public void IndexedConstructor_UsesSharedChannelIndex()
    {
        var pressureId = Guid.NewGuid();
        var rateId = Guid.NewGuid();

        var index = new ChannelIndex(
        [
            pressureId,
            rateId
        ]);

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            index,
            [8500.0, 42.5]);

        Assert.Equal(8500.0, sample.GetValue(pressureId));
        Assert.Equal(42.5, sample.GetValue(rateId));
    }

    [Fact]
    public void MultipleSamples_CanShareChannelIndex()
    {
        var channelId = Guid.NewGuid();
        var index = new ChannelIndex([channelId]);

        var first = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            index,
            [100.0]);

        var second = new TelemetrySample(
            1,
            new TelemetryTimestamp(200),
            index,
            [200.0]);

        Assert.Equal(100.0, first.GetValue(channelId));
        Assert.Equal(200.0, second.GetValue(channelId));
    }

    [Fact]
    public void IndexedConstructor_CopiesInputValues()
    {
        var channelId = Guid.NewGuid();
        var index = new ChannelIndex([channelId]);

        double?[] values = [100.0];

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            index,
            values);

        values[0] = 999.0;

        Assert.Equal(100.0, sample.GetValue(channelId));
    }

    [Fact]
    public void IndexedConstructor_RejectsMismatchedValueCount()
    {
        var index = new ChannelIndex(
        [
            Guid.NewGuid(),
            Guid.NewGuid()
        ]);

        Assert.Throws<ArgumentException>(() =>
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                index,
                [100.0]));
    }

    [Fact]
    public void IndexedConstructor_PreservesNullValues()
    {
        var channelId = Guid.NewGuid();
        var index = new ChannelIndex([channelId]);

        var sample = new TelemetrySample(
            0,
            new TelemetryTimestamp(100),
            index,
            [null]);

        Assert.True(sample.HasChannel(channelId));
        Assert.Null(sample.GetValue(channelId));
    }
}
