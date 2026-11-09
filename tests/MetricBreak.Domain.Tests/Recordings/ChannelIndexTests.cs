
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class ChannelIndexTests
{
    [Fact]
    public void Constructor_AssignsPositionsInInputOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var index = new ChannelIndex(
        [
            first,
            second,
            third
        ]);

        Assert.Equal(3, index.Count);
        Assert.Equal(0, index.GetPosition(first));
        Assert.Equal(1, index.GetPosition(second));
        Assert.Equal(2, index.GetPosition(third));
    }

    [Fact]
    public void Constructor_EmptyCollection_CreatesEmptyIndex()
    {
        var index = new ChannelIndex(
            Array.Empty<Guid>());

        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void Constructor_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ChannelIndex(null!));
    }

    [Fact]
    public void Constructor_EmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ChannelIndex(
            [
                Guid.Empty
            ]));
    }

    [Fact]
    public void Constructor_DuplicateChannelId_Throws()
    {
        var channelId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            () => new ChannelIndex(
            [
                channelId,
                channelId
            ]));
    }

    [Fact]
    public void GetPosition_UnknownChannel_Throws()
    {
        var index = new ChannelIndex(
        [
            Guid.NewGuid()
        ]);

        Assert.Throws<KeyNotFoundException>(
            () => index.GetPosition(Guid.NewGuid()));
    }

    [Fact]
    public void TryGetPosition_KnownChannel_ReturnsTrue()
    {
        var channelId = Guid.NewGuid();

        var index = new ChannelIndex(
        [
            channelId
        ]);

        var found = index.TryGetPosition(
            channelId,
            out var position);

        Assert.True(found);
        Assert.Equal(0, position);
    }

    [Fact]
    public void TryGetPosition_UnknownChannel_ReturnsFalse()
    {
        var index = new ChannelIndex(
        [
            Guid.NewGuid()
        ]);

        var found = index.TryGetPosition(
            Guid.NewGuid(),
            out var position);

        Assert.False(found);
        Assert.Equal(0, position);
    }

    [Fact]
    public void Contains_KnownChannel_ReturnsTrue()
    {
        var channelId = Guid.NewGuid();

        var index = new ChannelIndex(
        [
            channelId
        ]);

        Assert.True(index.Contains(channelId));
    }

    [Fact]
    public void Contains_UnknownChannel_ReturnsFalse()
    {
        var index = new ChannelIndex(
        [
            Guid.NewGuid()
        ]);

        Assert.False(index.Contains(Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_PreservesOrderFromDeferredEnumerable()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        IEnumerable<Guid> Channels()
        {
            yield return first;
            yield return second;
        }

        var index = new ChannelIndex(Channels());

        Assert.Equal(0, index.GetPosition(first));
        Assert.Equal(1, index.GetPosition(second));
    }
}
