
using System.Security.Cryptography;
using System.Text;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion.Csv;

public sealed class CsvReplayIntegrationTests
{
    [Fact]
    public void CsvRecording_RunToBreakpoint_StopsAtExpectedSample()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500,72
            2026-11-08T10:00:01Z,8800,70
            2026-11-08T10:00:02Z,9100,68
            2026-11-08T10:00:03Z,9300,55
            """;

        // Arrange: import CSV telemetry.
        var bytes = Encoding.UTF8.GetBytes(csv);
        using var textReader = new StringReader(csv);

        var parser = new CsvTelemetryParser();
        var result = parser.Parse(textReader);

        // Create a recording with source identity.
        var recordingId = Guid.NewGuid();

        var source = new RecordingSource(
            "treatment.csv",
            Convert.ToHexString(SHA256.HashData(bytes)),
            bytes.LongLength);

        var recording = new Recording(
            recordingId,
            "Treatment Recording",
            source,
            DateTimeOffset.UtcNow);

        // Connect imported telemetry to the replay engine.
        var reader = new CsvTelemetryReader(recordingId, result);
        var session = new ReplaySession(recording, reader);

        // Register a conditional breakpoint.
        var pressureChannel = Assert.Single(
            result.Channels,
            channel => channel.Name == "Pressure");

        var breakpoint = new ConditionalBreakpoint(
            Guid.NewGuid(),
            pressureChannel.Id,
            ComparisonOperator.GreaterThan,
            9000);

        var breakpoints = new BreakpointCollection();
        breakpoints.Add(breakpoint);

        // Act: run until the first breakpoint matches.
        var stop = session.RunToBreakpoints(breakpoints);

        // Assert: replay stops at the correct position.
        Assert.NotNull(stop);

        Assert.Equal(recordingId, stop.Position.RecordingId);
        Assert.Equal(2, stop.Position.SequenceNumber);

        Assert.Equal(2, session.Position.SequenceNumber);
        Assert.Equal(9100d, session.CurrentSample.GetValue(
            pressureChannel.Id));

        // Verify the breakpoint evidence.
        var hit = Assert.Single(stop.Hits);

        Assert.Equal(breakpoint.Id, hit.BreakpointId);
        Assert.Equal(pressureChannel.Id, hit.ChannelId);
        Assert.Equal(9100d, hit.ObservedValue);
        Assert.Equal(ComparisonOperator.GreaterThan, hit.Operator);
        Assert.Equal(9000d, hit.Threshold);

        Assert.Equal(session.CurrentSample.Timestamp, hit.Timestamp);
        Assert.Equal(stop.Position, hit.Position);

        // Verify source identity.
        Assert.Equal("treatment.csv", recording.Source.Name);
        Assert.Equal(bytes.LongLength, recording.Source.SizeInBytes);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(bytes)),
            recording.Source.ContentHash);
    }

    [Fact]
    public void CsvRecording_AfterBreakpoint_CanStepBackwardAndForward()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,8500
            2026-11-08T10:00:01Z,8800
            2026-11-08T10:00:02Z,9100
            """;

        using var textReader = new StringReader(csv);
        var result = new CsvTelemetryParser().Parse(textReader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Treatment",
            new RecordingSource("treatment.csv", "test-hash", csv.Length),
            DateTimeOffset.UtcNow);

        var reader = new CsvTelemetryReader(recordingId, result);
        var session = new ReplaySession(recording, reader);

        var pressureId = result.Channels[0].Id;

        var breakpoint = new ConditionalBreakpoint(
            Guid.NewGuid(),
            pressureId,
            ComparisonOperator.GreaterThan,
            9000);

        var stop = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(stop);
        Assert.Equal(2, session.Position.SequenceNumber);

        // Rewind one sample.
        Assert.True(session.StepBackward());

        Assert.Equal(1, session.Position.SequenceNumber);
        Assert.Equal(8800d, session.CurrentSample.GetValue(pressureId));

        // Replay forward again.
        Assert.True(session.StepForward());

        Assert.Equal(2, session.Position.SequenceNumber);
        Assert.Equal(9100d, session.CurrentSample.GetValue(pressureId));
    }

    [Fact]
    public void CsvRecording_DuplicateTimestamps_ReplayUsesSequenceNumbers()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,8500
            2026-11-08T10:00:00Z,9100
            2026-11-08T10:00:00Z,9300
            """;

        using var textReader = new StringReader(csv);
        var result = new CsvTelemetryParser().Parse(textReader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Treatment",
            new RecordingSource("treatment.csv", "test-hash", csv.Length),
            DateTimeOffset.UtcNow);

        var reader = new CsvTelemetryReader(recordingId, result);
        var session = new ReplaySession(recording, reader);

        var breakpoint = new ConditionalBreakpoint(
            Guid.NewGuid(),
            result.Channels[0].Id,
            ComparisonOperator.GreaterThan,
            9000);

        var firstHit = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(firstHit);
        Assert.Equal(1, firstHit.Position.SequenceNumber);

        var secondHit = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(secondHit);
        Assert.Equal(2, secondHit.Position.SequenceNumber);

        Assert.Equal(firstHit.Timestamp, secondHit.Timestamp);
    }
}
