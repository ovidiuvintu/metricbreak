
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ReplaySessionManagerStepForwardTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public void StepForward_FromSequence0_MovesToSequence1()
    {
        var (manager, sessionId) = CreateSession();

        var found = manager.TryStepForwardWithSnapshot(
            sessionId,
            out var advanced,
            out var snapshot);

        Assert.True(found);
        Assert.True(advanced);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.CurrentSample.SequenceNumber);
        Assert.Equal(ReplayExecutionStatus.Paused,
            snapshot.ExecutionState.Status);
        Assert.Equal(ReplayPauseReason.User,
            snapshot.ExecutionState.PauseReason);
    }

    [Fact]
    public void StepForward_FromSequence1_MovesToSequence2()
    {
        var (manager, sessionId) = CreateSession();

        StepForward(manager, sessionId);

        var found = manager.TryStepForwardWithSnapshot(
            sessionId,
            out var advanced,
            out var snapshot);

        Assert.True(found);
        Assert.True(advanced);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void StepForward_AtLastSequence_DoesNotAdvance()
    {
        var (manager, sessionId) = CreateSession();

        StepForward(manager, sessionId);
        StepForward(manager, sessionId);

        var found = manager.TryStepForwardWithSnapshot(
            sessionId,
            out var advanced,
            out var snapshot);

        Assert.True(found);
        Assert.False(advanced);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.CurrentSample.SequenceNumber);
        Assert.Equal(ReplayExecutionStatus.Paused,
            snapshot.ExecutionState.Status);
        Assert.Equal(ReplayPauseReason.User,
            snapshot.ExecutionState.PauseReason);
    }

    [Fact]
    public void StepForward_ImmediatelyAfterCreation_ChangesReadyToPaused()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryGetSnapshot(
            sessionId,
            out var initialSnapshot));

        Assert.NotNull(initialSnapshot);
        Assert.Equal(0, initialSnapshot.CurrentSample.SequenceNumber);
        Assert.Equal(ReplayExecutionStatus.Ready,
            initialSnapshot.ExecutionState.Status);
        Assert.Equal(ReplayPauseReason.None,
            initialSnapshot.ExecutionState.PauseReason);

        var found = manager.TryStepForwardWithSnapshot(
            sessionId,
            out var advanced,
            out var snapshot);

        Assert.True(found);
        Assert.True(advanced);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.CurrentSample.SequenceNumber);
        Assert.Equal(ReplayExecutionStatus.Paused,
            snapshot.ExecutionState.Status);
        Assert.Equal(ReplayPauseReason.User,
            snapshot.ExecutionState.PauseReason);
    }

    [Fact]
    public void StepBackward_AfterStepForward_MovesBackward()
    {
        var (manager, sessionId) = CreateSession();

        StepForward(manager, sessionId);
        StepForward(manager, sessionId);

        var found = manager.TryStepBackwardWithSnapshot(
            sessionId,
            out var movedBackward,
            out var snapshot);

        Assert.True(found);
        Assert.True(movedBackward);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.CurrentSample.SequenceNumber);
    }

    [Fact]
    public async Task ConcurrentStepForward_DoesNotMoveBeyondLastSequence()
    {
        var (manager, sessionId) = CreateSession();

        const int requestCount = 100;

        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, requestCount)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();

                var found = manager.TryStepForwardWithSnapshot(
                    sessionId,
                    out var advanced,
                    out var snapshot);

                return (found, advanced, snapshot);
            }))
            .ToArray();

        start.Set();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result =>
        {
            Assert.True(result.found);
            Assert.NotNull(result.snapshot);

            Assert.InRange(
                result.snapshot.CurrentSample.SequenceNumber,
                0,
                2);
        });

        Assert.Equal(
            2,
            results.Count(result => result.advanced));

        Assert.True(manager.TryGetSnapshot(
            sessionId,
            out var finalSnapshot));

        Assert.NotNull(finalSnapshot);
        Assert.Equal(2, finalSnapshot.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void StepForward_ReturnsCorrectTelemetrySample()
    {
        var (manager, sessionId) = CreateSession();

        var found = manager.TryStepForwardWithSnapshot(
            sessionId,
            out var advanced,
            out var snapshot);

        Assert.True(found);
        Assert.True(advanced);
        Assert.NotNull(snapshot);

        Assert.Equal(1, snapshot.CurrentSample.SequenceNumber);

        Assert.Equal(
            1794132001000000000L,
            snapshot.CurrentSample.Timestamp.UnixNanoseconds);

        Assert.Contains(
            snapshot.CurrentSample.Values.Values,
            value => value == 8700);

        Assert.Contains(
            snapshot.CurrentSample.Values.Values,
            value => value == 42);
    }

    private static void StepForward(
        ReplaySessionManager manager,
        Guid sessionId)
    {
        Assert.True(manager.TryStepForwardWithSnapshot(
            sessionId,
            out var advanced,
            out _));

        Assert.True(advanced);
    }

    private static (
        ReplaySessionManager Manager,
        Guid SessionId) CreateSession()
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(_csvContent);

        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Step Forward Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                _csvContent.Length),
            DateTimeOffset.UtcNow);

        var telemetryReader = new CsvTelemetryReader(
            recordingId,
            parsed);

        var imported = new ImportedCsvRecording(
            recording,
            parsed.Channels,
            telemetryReader);

        var manager = new ReplaySessionManager();

        var sessionId = manager.Create(imported);

        return (manager, sessionId);
    }
}
