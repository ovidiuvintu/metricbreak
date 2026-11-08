
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class TelemetryTimestampTests
{
    [Fact]
    public void FromUnixMilliseconds_ConvertsToNanoseconds()
    {
        var timestamp =
            TelemetryTimestamp.FromUnixMilliseconds(1500);

        Assert.Equal(
            1_500_000_000L,
            timestamp.UnixNanoseconds);
    }

    [Fact]
    public void FromUnixMicroseconds_ConvertsToNanoseconds()
    {
        var timestamp =
            TelemetryTimestamp.FromUnixMicroseconds(1500);

        Assert.Equal(
            1_500_000L,
            timestamp.UnixNanoseconds);
    }

    [Fact]
    public void FromDateTimeOffset_NormalizesToUtc()
    {
        var value = new DateTimeOffset(
            2026, 11, 8, 10, 0, 0,
            TimeSpan.FromHours(-6));

        var timestamp =
            TelemetryTimestamp.FromDateTimeOffset(value);

        Assert.Equal(
            value.ToUnixTimeMilliseconds() * 1_000_000,
            timestamp.UnixNanoseconds);
    }

    [Fact]
    public void NanosecondPrecision_IsPreserved()
    {
        var timestamp = new TelemetryTimestamp(
            1_000_000_123);

        Assert.Equal(
            1_000_000_123L,
            timestamp.UnixNanoseconds);
    }

    [Fact]
    public void CompareTo_OrdersTimestamps()
    {
        var earlier = new TelemetryTimestamp(100);
        var later = new TelemetryTimestamp(200);

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
        Assert.Equal(0, earlier.CompareTo(earlier));
    }

    [Fact]
    public void DifferenceInNanoseconds_ReturnsExactDifference()
    {
        var earlier = new TelemetryTimestamp(100);
        var later = new TelemetryTimestamp(350);

        var difference =
            TelemetryTimestamp.DifferenceInNanoseconds(
                later, earlier);

        Assert.Equal(250L, difference);
    }

    [Fact]
    public void ToDateTimeOffset_RoundsDownTo100Nanoseconds()
    {
        var timestamp = new TelemetryTimestamp(199);

        var result = timestamp.ToDateTimeOffset();

        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddTicks(1),
            result);
    }

    [Fact]
    public void ToDateTimeOffset_NegativeNanoseconds_RoundsDown()
    {
        var timestamp = new TelemetryTimestamp(-1);

        var result = timestamp.ToDateTimeOffset();

        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddTicks(-1),
            result);
    }

    [Fact]
    public void FromUnixMilliseconds_WhenOverflow_Throws()
    {
        Assert.Throws<OverflowException>(() =>
            TelemetryTimestamp.FromUnixMilliseconds(
                long.MaxValue));
    }

    [Fact]
    public void FromDateTimeOffset_WhenOutsideNanosecondRange_Throws()
    {
        Assert.Throws<OverflowException>(() =>
            TelemetryTimestamp.FromDateTimeOffset(
                DateTimeOffset.MaxValue));
    }
}
