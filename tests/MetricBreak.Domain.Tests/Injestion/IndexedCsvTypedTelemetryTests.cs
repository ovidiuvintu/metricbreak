using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion;

public sealed class IndexedCsvTypedTelemetryTests
{
    [Fact]
    public void MixedColumns_AreInferredAndValuesPreserved()
    {
        const string csv = "Timestamp,Pressure,ValveState,PumpRunning\n" +
            "2026-11-15T10:00:00Z,4200,OFF,true\n" +
            "2026-11-15T10:00:01Z,4500,ON,false\n";

        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));

        Assert.Equal(ChannelValueType.Numeric, result.Channels[0].ValueType);
        Assert.Equal(ChannelValueType.Categorical, result.Channels[1].ValueType);
        Assert.Equal(ChannelValueType.Boolean, result.Channels[2].ValueType);
        Assert.Equal(4200d, result.Samples[0].GetValue(result.Channels[0].Id));
        Assert.Equal("OFF", Assert.IsType<ChannelValue.Categorical>(
            result.Samples[0].GetTypedValue(result.Channels[1].Id)).Value);
        Assert.True(Assert.IsType<ChannelValue.Boolean>(
            result.Samples[0].GetTypedValue(result.Channels[2].Id)).Value);
        Assert.False(Assert.IsType<ChannelValue.Boolean>(
            result.Samples[1].GetTypedValue(result.Channels[2].Id)).Value);
        Assert.False(result.Samples[0].Values.ContainsKey(result.Channels[1].Id));
    }

    [Fact]
    public void TypeInference_UsesAllRows_NotFirstRow()
    {
        const string csv = "Timestamp,State\n" +
            "2026-11-15T10:00:00Z,1\n" +
            "2026-11-15T10:00:01Z,ON\n";
        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));
        Assert.Equal(ChannelValueType.Categorical, result.Channels[0].ValueType);
        Assert.Equal("1", Assert.IsType<ChannelValue.Categorical>(
            result.Samples[0].GetTypedValue(result.Channels[0].Id)).Value);
    }

    [Fact]
    public void MissingMeasurements_AreDistinctFromFalseAndZero()
    {
        const string csv = "Timestamp,Pressure,Enabled,Mode\n" +
            "2026-11-15T10:00:00Z,0,false,OFF\n" +
            "2026-11-15T10:00:01Z,,,\n";
        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));
        foreach (var channel in result.Channels)
        {
            Assert.True(result.Samples[1].HasChannel(channel.Id));
            Assert.Null(result.Samples[1].GetTypedValue(channel.Id));
        }
        Assert.Equal(0d, result.Samples[0].GetValue(result.Channels[0].Id));
    }

    [Fact]
    public void QuotedCategoricalFields_PreserveCommasAndQuotes()
    {
        const string csv = "Timestamp,Mode\n" +
            "2026-11-15T10:00:00Z,\"AUTO, \"\"SAFE\"\"\"\n";
        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));
        Assert.Equal("AUTO, \"SAFE\"", Assert.IsType<ChannelValue.Categorical>(
            result.Samples[0].GetTypedValue(result.Channels[0].Id)).Value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void NonFiniteTokens_AreRejected(string token)
    {
        var csv = $"Timestamp,Pressure\n2026-11-15T10:00:00Z,{token}\n";
        Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));
    }

    [Fact]
    public void EntirelyEmptyChannel_IsRejected()
    {
        const string csv = "Timestamp,Pressure\n2026-11-15T10:00:00Z,\n";
        Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));
    }
}
