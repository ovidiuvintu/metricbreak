
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ReplaySessionManagerResetTests
{
    private const string CsvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public void Reset_AfterStepForward_RestoresInitialState()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryStepForwardWithSnapshot(
            sessionId, out var advanced, out var stepped));

        Assert.True(advanced);
        Assert.NotNull(stepped);
        Assert.Equal(1, stepped.CurrentSample.SequenceNumber);

        var found = manager.TryResetWithSnapshot(
            sessionId, out var snapshot);

        Assert.True(found);
        AssertInitialState(snapshot);
    }

    [Fact]
    public void Reset_AfterStepBackward_RestoresInitialState()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryStepForwardWithSnapshot(
            sessionId, out _, out _));

        Assert.True(manager.TryStepBackwardWithSnapshot(
            sessionId, out var movedBackward, out var stepped));

        Assert.True(movedBackward);
        Assert.NotNull(stepped);
        Assert.Equal(0, stepped.CurrentSample.SequenceNumber);
        Assert.Equal(
            ReplayExecutionStatus.Paused,
            stepped.ExecutionState.Status);

        var found = manager.TryResetWithSnapshot(
            sessionId, out var snapshot);

        Assert.True(found);
        AssertInitialState(snapshot);
    }

    [Fact]
    public void Reset_AfterSeek_RestoresInitialState()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TrySeekWithSnapshot(
            sessionId, 2, out var sought, out var invalidPosition));

        Assert.False(invalidPosition);
        Assert.NotNull(sought);
        Assert.Equal(2, sought.CurrentSample.SequenceNumber);

        var found = manager.TryResetWithSnapshot(
            sessionId, out var snapshot);

        Assert.True(found);
        AssertInitialState(snapshot);
    }

    [Fact]
    public void Reset_UntouchedSession_Succeeds()
    {
        var (manager, sessionId) = CreateSession();

        var found = manager.TryResetWithSnapshot(
            sessionId, out var snapshot);

        Assert.True(found);
        AssertInitialState(snapshot);
    }

    [Fact]
    public void Reset_Twice_IsIdempotent()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryStepForwardWithSnapshot(
            sessionId, out _, out _));

        var firstFound = manager.TryResetWithSnapshot(
            sessionId, out var firstSnapshot);

        var secondFound = manager.TryResetWithSnapshot(
            sessionId, out var secondSnapshot);

        Assert.True(firstFound);
        Assert.True(secondFound);

        AssertInitialState(firstSnapshot);
        AssertInitialState(secondSnapshot);

        Assert.Equal(
            firstSnapshot!.RecordingId,
            secondSnapshot!.RecordingId);

        Assert.Equal(
            firstSnapshot.SessionId,
            secondSnapshot.SessionId);
    }

    [Fact]
    public void Reset_UnknownSession_ReturnsFalse()
    {
        var manager = new ReplaySessionManager();

        var found = manager.TryResetWithSnapshot(
            Guid.NewGuid(), out var snapshot);

        Assert.False(found);
        Assert.Null(snapshot);
    }

    [Fact]
    public void Reset_RemovedSession_ReturnsFalse()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.Remove(sessionId));

        var found = manager.TryResetWithSnapshot(
            sessionId, out var snapshot);

        Assert.False(found);
        Assert.Null(snapshot);
    }

    [Fact]
    public void Reset_FromLastSample_RestoresInitialState()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryStepForwardWithSnapshot(
            sessionId, out var firstAdvanced, out _));

        Assert.True(firstAdvanced);

        Assert.True(manager.TryStepForwardWithSnapshot(
            sessionId, out var secondAdvanced, out var lastSnapshot));

        Assert.True(secondAdvanced);
        Assert.NotNull(lastSnapshot);
        Assert.Equal(2, lastSnapshot.CurrentSample.SequenceNumber);

        var found = manager.TryResetWithSnapshot(
            sessionId, out var snapshot);

        Assert.True(found);
        AssertInitialState(snapshot);
    }

    [Fact]
    public async Task ConcurrentReset_AllSnapshotsAreConsistent()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TrySeekWithSnapshot(
            sessionId, 2, out _, out var invalidPosition));

        Assert.False(invalidPosition);

        const int requestCount = 100;

        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, requestCount)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();

                var found = manager.TryResetWithSnapshot(
                    sessionId, out var snapshot);

                return (Found: found, Snapshot: snapshot);
            }))
            .ToArray();

        start.Set();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result =>
        {
            Assert.True(result.Found);
            AssertInitialState(result.Snapshot);
        });

        Assert.True(manager.TryGetSnapshot(
            sessionId, out var finalSnapshot));

        AssertInitialState(finalSnapshot);
    }

    [Fact]
    public void Reset_PreservesRecordingMetadata()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryGetSnapshot(
            sessionId, out var initialSnapshot));

        Assert.NotNull(initialSnapshot);

        Assert.True(manager.TrySeekWithSnapshot(
            sessionId, 2, out _, out var invalidPosition));

        Assert.False(invalidPosition);

        var found = manager.TryResetWithSnapshot(
            sessionId, out var resetSnapshot);

        Assert.True(found);
        AssertInitialState(resetSnapshot);

        Assert.Equal(
            initialSnapshot.SessionId,
            resetSnapshot!.SessionId);

        Assert.Equal(
            initialSnapshot.RecordingId,
            resetSnapshot.RecordingId);

        Assert.Equal(
            initialSnapshot.RecordingName,
            resetSnapshot.RecordingName);

        Assert.Equal(
            initialSnapshot.SampleCount,
            resetSnapshot.SampleCount);
    }

    private static void AssertInitialState(
        ReplaySessionSnapshot? snapshot)
    {
        Assert.NotNull(snapshot);

        Assert.Equal(
            0,
            snapshot.CurrentSample.SequenceNumber);

        Assert.Equal(
            0,
            snapshot.Position.SequenceNumber);

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            snapshot.ExecutionState.Status);

        Assert.Equal(
            ReplayPauseReason.None,
            snapshot.ExecutionState.PauseReason);

        Assert.Equal(3, snapshot.SampleCount);
    }

    private static (
        ReplaySessionManager Manager,
        Guid SessionId) CreateSession()
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(CsvContent);

        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Reset Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                CsvContent.Length),
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
