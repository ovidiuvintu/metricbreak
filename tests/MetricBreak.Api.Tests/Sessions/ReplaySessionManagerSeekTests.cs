
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ReplaySessionManagerSeekTests
{
    private const string CsvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public void Seek_FromSequence0_ToSequence2_MovesToSequence2()
    {
        var (manager, sessionId) = CreateSession();

        var found = manager.TrySeekWithSnapshot(
            sessionId, 2, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.False(invalidPosition);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.CurrentSample.SequenceNumber);
        Assert.Equal(ReplayExecutionStatus.Paused,
            snapshot.ExecutionState.Status);
        Assert.Equal(ReplayPauseReason.User,
            snapshot.ExecutionState.PauseReason);
    }

    [Fact]
    public void Seek_BackwardFromSequence2_ToSequence0_MovesToSequence0()
    {
        var (manager, sessionId) = CreateSession();

        Seek(manager, sessionId, 2);

        var found = manager.TrySeekWithSnapshot(
            sessionId, 0, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.False(invalidPosition);
        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void Seek_ToCurrentSequence_Succeeds()
    {
        var (manager, sessionId) = CreateSession();

        Seek(manager, sessionId, 1);

        var found = manager.TrySeekWithSnapshot(
            sessionId, 1, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.False(invalidPosition);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void Seek_NegativeSequence_ReturnsInvalidPosition()
    {
        var (manager, sessionId) = CreateSession();

        Seek(manager, sessionId, 1);

        var found = manager.TrySeekWithSnapshot(
            sessionId, -1, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.True(invalidPosition);
        Assert.Null(snapshot);

        AssertPosition(manager, sessionId, 1);
    }

    [Fact]
    public void Seek_SequenceEqualToSampleCount_ReturnsInvalidPosition()
    {
        var (manager, sessionId) = CreateSession();

        Seek(manager, sessionId, 1);

        var found = manager.TrySeekWithSnapshot(
            sessionId, 3, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.True(invalidPosition);
        Assert.Null(snapshot);

        AssertPosition(manager, sessionId, 1);
    }

    [Fact]
    public void Seek_UnknownSession_ReturnsFalse()
    {
        var manager = new ReplaySessionManager();

        var found = manager.TrySeekWithSnapshot(
            Guid.NewGuid(), 1, out var snapshot, out var invalidPosition);

        Assert.False(found);
        Assert.False(invalidPosition);
        Assert.Null(snapshot);
    }

    [Fact]
    public void Seek_RemovedSession_ReturnsFalse()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.Remove(sessionId));

        var found = manager.TrySeekWithSnapshot(
            sessionId, 1, out var snapshot, out var invalidPosition);

        Assert.False(found);
        Assert.False(invalidPosition);
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task ConcurrentSeek_EachSnapshotMatchesRequestedPosition()
    {
        var (manager, sessionId) = CreateSession();

        const int requestCount = 100;

        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, requestCount)
            .Select(i => Task.Run(() =>
            {
                var requestedSequence = (long)(i % 3);

                start.Wait();

                var found = manager.TrySeekWithSnapshot(
                    sessionId,
                    requestedSequence,
                    out var snapshot,
                    out var invalidPosition);

                return (
                    RequestedSequence: requestedSequence,
                    Found: found,
                    InvalidPosition: invalidPosition,
                    Snapshot: snapshot);
            }))
            .ToArray();

        start.Set();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result =>
        {
            Assert.True(result.Found);
            Assert.False(result.InvalidPosition);
            Assert.NotNull(result.Snapshot);

            Assert.Equal(
                result.RequestedSequence,
                result.Snapshot.CurrentSample.SequenceNumber);
        });

        Assert.True(manager.TryGetSnapshot(
            sessionId,
            out var finalSnapshot));

        Assert.NotNull(finalSnapshot);

        Assert.InRange(
            finalSnapshot.CurrentSample.SequenceNumber,
            0,
            2);
    }

    [Fact]
    public void Seek_ReturnsCorrectTelemetrySample()
    {
        var (manager, sessionId) = CreateSession();

        var found = manager.TrySeekWithSnapshot(
            sessionId, 2, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.False(invalidPosition);
        Assert.NotNull(snapshot);

        Assert.Equal(2, snapshot.CurrentSample.SequenceNumber);

        Assert.Equal(
            1794132002000000000L,
            snapshot.CurrentSample.Timestamp.UnixNanoseconds);

        Assert.Contains(
            snapshot.CurrentSample.Values.Values,
            value => value == 9500);

        Assert.Contains(
            snapshot.CurrentSample.Values.Values,
            value => value == 45);
    }

    [Fact]
    public void Seek_InvalidPosition_DoesNotChangeExecutionState()
    {
        var (manager, sessionId) = CreateSession();

        Assert.True(manager.TryGetSnapshot(
            sessionId, out var initialSnapshot));

        Assert.NotNull(initialSnapshot);
        Assert.Equal(ReplayExecutionStatus.Ready,
            initialSnapshot.ExecutionState.Status);

        var found = manager.TrySeekWithSnapshot(
            sessionId, 100, out var snapshot, out var invalidPosition);

        Assert.True(found);
        Assert.True(invalidPosition);
        Assert.Null(snapshot);

        Assert.True(manager.TryGetSnapshot(
            sessionId, out var finalSnapshot));

        Assert.NotNull(finalSnapshot);

        Assert.Equal(0, finalSnapshot.CurrentSample.SequenceNumber);
        Assert.Equal(ReplayExecutionStatus.Ready,
            finalSnapshot.ExecutionState.Status);
        Assert.Equal(ReplayPauseReason.None,
            finalSnapshot.ExecutionState.PauseReason);
    }

    private static void Seek(
        ReplaySessionManager manager,
        Guid sessionId,
        long sequenceNumber)
    {
        var found = manager.TrySeekWithSnapshot(
            sessionId,
            sequenceNumber,
            out var snapshot,
            out var invalidPosition);

        Assert.True(found);
        Assert.False(invalidPosition);
        Assert.NotNull(snapshot);
        Assert.Equal(sequenceNumber, snapshot.CurrentSample.SequenceNumber);
    }

    private static void AssertPosition(
        ReplaySessionManager manager,
        Guid sessionId,
        long expectedSequence)
    {
        Assert.True(manager.TryGetSnapshot(
            sessionId, out var snapshot));

        Assert.NotNull(snapshot);
        Assert.Equal(
            expectedSequence,
            snapshot.CurrentSample.SequenceNumber);
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
            "Seek Test",
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

