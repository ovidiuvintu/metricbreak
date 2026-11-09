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

        if (!_sessions.TryAdd(sessionId, new SessionEntry(controller)))
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

    private sealed class SessionEntry
    {
        public ReplayExecutionController Controller { get; }

        public object SyncRoot { get; } = new();

        public bool IsClosed { get; set; }

        public SessionEntry(ReplayExecutionController controller)
        {
            Controller = controller;
        }
    }
}
