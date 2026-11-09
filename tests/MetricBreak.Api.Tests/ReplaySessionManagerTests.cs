
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ReplaySessionManagerTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public void Create_ReturnsNonEmptySessionId()
    {
        var manager = new ReplaySessionManager();

        var sessionId = manager.Create(CreateImportedRecording());

        Assert.NotEqual(Guid.Empty, sessionId);
    }

    [Fact]
    public void Create_TwoSessions_ReturnsDifferentIds()
    {
        var manager = new ReplaySessionManager();

        var first = manager.Create(CreateImportedRecording());
        var second = manager.Create(CreateImportedRecording());

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_NullRecording_Throws()
    {
        var manager = new ReplaySessionManager();

        Assert.Throws<ArgumentNullException>(
            () => manager.Create(null!));
    }

    [Fact]
    public void TryExecute_AdvancesSession()
    {
        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(CreateImportedRecording());

        var found = manager.TryExecute(
            sessionId,
            controller => controller.StepForward(),
            out var advanced);

        Assert.True(found);
        Assert.True(advanced);

        var foundAgain = manager.TryExecute(
            sessionId,
            controller => controller.Position.SequenceNumber,
            out var position);

        Assert.True(foundAgain);
        Assert.Equal(1, position);
    }

    [Fact]
    public void TryExecute_UnknownSession_ReturnsFalse()
    {
        var manager = new ReplaySessionManager();

        var found = manager.TryExecute(
            Guid.NewGuid(),
            controller => controller.StepForward(),
            out var advanced);

        Assert.False(found);
        Assert.False(advanced);
    }

    [Fact]
    public void TryExecute_NullOperation_Throws()
    {
        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(CreateImportedRecording());

        Assert.Throws<ArgumentNullException>(() =>
            manager.TryExecute<bool>(
                sessionId,
                null!,
                out _));
    }

    [Fact]
    public void Remove_ExistingSession_ReturnsTrue()
    {
        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(CreateImportedRecording());

        Assert.True(manager.Remove(sessionId));
    }

    [Fact]
    public void Remove_UnknownSession_ReturnsFalse()
    {
        var manager = new ReplaySessionManager();

        Assert.False(manager.Remove(Guid.NewGuid()));
    }

    [Fact]
    public void Remove_Twice_SecondCallReturnsFalse()
    {
        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(CreateImportedRecording());

        Assert.True(manager.Remove(sessionId));
        Assert.False(manager.Remove(sessionId));
    }

    [Fact]
    public void TryExecute_AfterRemoval_ReturnsFalse()
    {
        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(CreateImportedRecording());

        manager.Remove(sessionId);

        var found = manager.TryExecute(
            sessionId,
            controller => controller.StepForward(),
            out var advanced);

        Assert.False(found);
        Assert.False(advanced);
    }

    [Fact]
    public void Sessions_HaveIndependentPositions()
    {
        var manager = new ReplaySessionManager();

        var first = manager.Create(CreateImportedRecording());
        var second = manager.Create(CreateImportedRecording());

        var found = manager.TryExecute(
            first,
            controller =>
            {
                controller.Seek(2);
                return true;
            },
            out var result);

        Assert.True(found);
        Assert.True(result);

        manager.TryExecute(
            first,
            controller => controller.Position.SequenceNumber,
            out var firstPosition);

        manager.TryExecute(
            second,
            controller => controller.Position.SequenceNumber,
            out var secondPosition);

        Assert.Equal(2, firstPosition);
        Assert.Equal(0, secondPosition);
    }

    [Fact]
    public async Task ConcurrentCommands_OnSameSession_AreSerialized()
    {
        var manager = new ReplaySessionManager();
        var sessionId = manager.Create(CreateImportedRecording());

        const int operationCount = 20;

        var activeOperations = 0;
        var maxConcurrentOperations = 0;

        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, operationCount)
            .Select(i => Task.Run(() =>
            {
                start.Wait();

                var found = manager.TryExecute<long>(
                    sessionId,
                    controller =>
                    {
                        var active = Interlocked.Increment(
                            ref activeOperations);

                        UpdateMaximum(
                            ref maxConcurrentOperations,
                            active);

                        try
                        {
                            Thread.Sleep(5);

                            return controller.Position.SequenceNumber;
                        }
                        finally
                        {
                            Interlocked.Decrement(
                                ref activeOperations);
                        }
                    },
                    out var position);

                Assert.True(found);
                Assert.Equal(0L, position);
            }))
            .ToArray();

        start.Set();

        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrentOperations);
    }


    [Fact]
    public void Remove_DoesNotAffectOtherSessions()
    {
        var manager = new ReplaySessionManager();

        var first = manager.Create(CreateImportedRecording());
        var second = manager.Create(CreateImportedRecording());

        Assert.True(manager.Remove(first));

        var firstFound = manager.TryExecute(
            first,
            controller => controller.Position.SequenceNumber,
            out _);

        var secondFound = manager.TryExecute(
            second,
            controller => controller.Position.SequenceNumber,
            out var position);

        Assert.False(firstFound);
        Assert.True(secondFound);
        Assert.Equal(0, position);
    }

    private static ImportedCsvRecording CreateImportedRecording()
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(_csvContent);

        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Session Manager Test",
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

    private static void UpdateMaximum(
        ref int maximum,
        int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);

            if (candidate <= current)
                return;

            if (Interlocked.CompareExchange(
                    ref maximum,
                    candidate,
                    current) == current)
            {
                return;
            }
        }
    }
}
