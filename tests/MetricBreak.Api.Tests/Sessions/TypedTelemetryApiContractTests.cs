using System.Text.Json;
using MetricBreak.Api.Sessions;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

/// <summary>
/// Exercises the real file importer, replay manager, and API response DTOs.
/// These are contract tests, not HTTP-host integration tests.
/// </summary>
public sealed class TypedTelemetryApiContractTests
{
    private const string Csv =
        "Timestamp,Pressure,PumpRunning,ValveState\n" +
        "2026-11-15T10:00:00Z,4500,true,OPEN\n" +
        "2026-11-15T10:00:01Z,4700,false,CLOSED\n" +
        "2026-11-15T10:00:02Z,,true,OPEN\n";

    [Fact]
    public void ImportedRecording_ExposesTypedChannelMetadata()
    {
        WithSession((manager, sessionId) =>
        {
            Assert.True(manager.TryGetChannels(sessionId, out var channels));
            Assert.NotNull(channels);
            Assert.Equal(3, channels.Count);
            Assert.Equal("Numeric", channels.Single(c => c.Name == "Pressure").ValueType);
            Assert.Equal("Boolean", channels.Single(c => c.Name == "PumpRunning").ValueType);
            Assert.Equal("Categorical", channels.Single(c => c.Name == "ValveState").ValueType);
        });
    }

    [Fact]
    public void CurrentSample_MapsTypedValuesAndPreservesNumericChannels()
    {
        WithSession((manager, sessionId) =>
        {
            var ids = ChannelIds(manager, sessionId);
            Assert.True(manager.TryGetSnapshot(sessionId, out var snapshot));
            Assert.NotNull(snapshot);
            var response = SessionStateResponseMapper.Map(snapshot);
            var sample = response.CurrentSample;
            Assert.Equal(0, sample.Sequence);
            Assert.Equal(4500d, sample.Channels[ids["Pressure"]]);
            Assert.Equal("Numeric", sample.TypedChannels[ids["Pressure"]].ValueType);
            Assert.Equal(4500d, Assert.IsType<double>(sample.TypedChannels[ids["Pressure"]].Value));
            Assert.Equal("Boolean", sample.TypedChannels[ids["PumpRunning"]].ValueType);
            Assert.True(Assert.IsType<bool>(sample.TypedChannels[ids["PumpRunning"]].Value));
            Assert.Equal("Categorical", sample.TypedChannels[ids["ValveState"]].ValueType);
            Assert.Equal("OPEN", Assert.IsType<string>(sample.TypedChannels[ids["ValveState"]].Value));
            Assert.False(sample.Channels.ContainsKey(ids["ValveState"]));
            Assert.False(sample.Channels.ContainsKey(ids["PumpRunning"]));
        });
    }

    [Fact]
    public void SampleRange_PreservesTypedValuesAndMissingMeasurements()
    {
        WithSession((manager, sessionId) =>
        {
            var ids = ChannelIds(manager, sessionId);

            Assert.True(manager.TryGetSampleRange(
                sessionId, 0, 3, out var range));

            Assert.NotNull(range);
            Assert.Equal(3, range.Count);
            Assert.Equal(3, range.Samples.Count);

            // Categorical values
            Assert.Equal(
                "CLOSED",
                range.Samples[1]
                    .TypedChannels[ids["ValveState"]].Value);

            // Boolean values
            Assert.Equal(
                false,
                range.Samples[1]
                    .TypedChannels[ids["PumpRunning"]].Value);

            // Missing numeric measurement
            Assert.False(
                range.Samples[2]
                    .Channels.ContainsKey(ids["Pressure"]));

            var pressure = range.Samples[2]
                .TypedChannels[ids["Pressure"]];

            Assert.Equal("Missing", pressure.ValueType);
            Assert.Null(pressure.Value);

            // Verify JSON serialization
            using var json = JsonDocument.Parse(
                JsonSerializer.Serialize(range));

            var pumpValue = json.RootElement
                .GetProperty("Samples")[2]
                .GetProperty("TypedChannels")
                .GetProperty(ids["PumpRunning"].ToString())
                .GetProperty("Value");

            var pressureValue = json.RootElement
                .GetProperty("Samples")[2]
                .GetProperty("TypedChannels")
                .GetProperty(ids["Pressure"].ToString())
                .GetProperty("Value");

            Assert.Equal(JsonValueKind.Null, pressureValue.ValueKind);

            Assert.Equal(JsonValueKind.True, pumpValue.ValueKind);
            Assert.True(pumpValue.GetBoolean());
        });
    }

    [Fact]
    public void Seek_UpdatesTypedCurrentSampleWithoutChangingRange()
    {
        WithSession((manager, sessionId) =>
        {
            var ids = ChannelIds(manager, sessionId);
            Assert.True(manager.TrySeekWithSnapshot(sessionId, 1, out var snapshot, out var invalid));
            Assert.False(invalid);
            Assert.NotNull(snapshot);
            var current = SessionStateResponseMapper.Map(snapshot).CurrentSample;
            Assert.Equal(1, current.Sequence);
            Assert.Equal("CLOSED", current.TypedChannels[ids["ValveState"]].Value);
            Assert.True(manager.TryGetSampleRange(sessionId, 0, 1, out var range));
            Assert.NotNull(range);
            Assert.Equal("OPEN", range.Samples[0].TypedChannels[ids["ValveState"]].Value);
        });
    }

    private static Dictionary<string, Guid> ChannelIds(ReplaySessionManager manager, Guid sessionId)
    {
        Assert.True(manager.TryGetChannels(sessionId, out var channels));
        Assert.NotNull(channels);
        return channels.ToDictionary(c => c.Name, c => c.ChannelId);
    }

    private static void WithSession(Action<ReplaySessionManager, Guid> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"metricbreak-typed-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path, Csv);
            var imported = new CsvRecordingImporter().Import(path);
            var manager = new ReplaySessionManager();
            var sessionId = manager.Create(imported);
            assertion(manager, sessionId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
