using System.Collections.Concurrent;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Sessions;

public sealed class ReplaySessionManager
{
    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();

    public Guid Create(ImportedCsvRecording imported)
    {
        ArgumentNullException.ThrowIfNull(imported);

        var session = new ReplaySession(
            imported.Recording,
            imported.Reader);

        var controller = new ReplayExecutionController(session);

        var sessionId = Guid.NewGuid();

        if (!_sessions.TryAdd(sessionId, new SessionEntry(controller, imported)))
        {
            throw new InvalidOperationException(
                "Unable to create replay session.");
        }

        return sessionId;
    }

    public bool TryExecute<T>(
        Guid sessionId,
        Func<ReplayExecutionController, T> operation,
        out T result)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            result = default!;
            return false;
        }

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
            {
                result = default!;
                return false;
            }

            result = operation(entry.Controller);
            return true;
        }
    }

    public bool Remove(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            entry.IsClosed = true;

            return _sessions.TryRemove(
                new KeyValuePair<Guid, SessionEntry>(
                    sessionId,
                    entry));
        }
    }

    private static ReplaySessionSnapshot CreateSnapshot(
        Guid sessionId,
        SessionEntry entry)
    {
        var controller = entry.Controller;
        var imported = entry.Imported;

        return new ReplaySessionSnapshot(
            SessionId: sessionId,
            RecordingId: imported.Recording.Id,
            RecordingName: imported.Recording.Name,
            SampleCount: imported.Reader.SampleCount,
            ExecutionState: controller.State,
            Position: controller.Position,
            CurrentSample: controller.CurrentSample);
    }

    public bool TryGetSnapshot(
        Guid sessionId,
        out ReplaySessionSnapshot? snapshot)
    {
        snapshot = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            snapshot = CreateSnapshot(sessionId, entry);
            return true;
        }
    }

    public bool TryStepForward(
        Guid sessionId,
        out bool advanced)
    {
        return TryExecute(
            sessionId,
            controller => controller.StepForward(),
            out advanced);
    }

    public bool TryStepForwardWithSnapshot(
        Guid sessionId,
        out bool advanced,
        out ReplaySessionSnapshot? snapshot)
    {
        advanced = false;
        snapshot = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            advanced = entry.Controller.StepForward();

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }

    public bool TryStepBackwardWithSnapshot(
        Guid sessionId,
        out bool movedBackward,
        out ReplaySessionSnapshot? snapshot)
    {
        movedBackward = false;
        snapshot = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            movedBackward = entry.Controller.StepBackward();

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }

    private sealed class SessionEntry
    {
        public ReplayExecutionController Controller { get; }

        public ImportedCsvRecording Imported { get; }

        public object SyncRoot { get; } = new();

        public bool IsClosed { get; set; }

        public SessionEntry(
            ReplayExecutionController controller,
            ImportedCsvRecording imported)
        {
            Controller = controller;
            Imported = imported;
        }
    }
}
