using System.Net;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class StepBackwardEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public async Task StepBackward_UnknownSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = Guid.NewGuid();

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/step-backward",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StepBackward_RemovedSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var manager = factory.SessionManager;

        var sessionId = manager.Create(CreateImportedRecording());

        Assert.True(manager.Remove(sessionId));

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/step-backward",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static ImportedCsvRecording CreateImportedRecording()
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(_csvContent);

        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Step Backward HTTP Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                _csvContent.Length),
            DateTimeOffset.UtcNow);

        var telemetryReader = new CsvTelemetryReader(
            recordingId,
            parsed);

        return new ImportedCsvRecording(
            recording,
            parsed.Channels,
            telemetryReader);
    }
}
