using System.Collections.Concurrent;
using MetricBreak.Domain.Breakpoints;
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

    public bool TryContinueWithSnapshot(
        Guid sessionId,
        out bool hadActiveBreakpoints,
        out BreakpointStop? stop,
        out ReplaySessionSnapshot? snapshot)
    {
        hadActiveBreakpoints = false;
        stop = null;
        snapshot = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            hadActiveBreakpoints = entry.Breakpoints.ActiveCount > 0;

            if (hadActiveBreakpoints)
            {
                stop = entry.Controller.Continue(entry.Breakpoints);
            }

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }

    public bool TrySetBreakpointEnabled(
        Guid sessionId,
        Guid breakpointId,
        bool isEnabled,
        out bool updated)
    {
        updated = false;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            if (breakpointId == Guid.Empty)
                return true;

            updated = isEnabled
                ? entry.Breakpoints.Enable(breakpointId)
                : entry.Breakpoints.Disable(breakpointId);

            return true;
        }
    }

    public bool TryAddBreakpoint(
        Guid sessionId,
        Guid channelId,
        ComparisonOperator comparisonOperator,
        double threshold,
        out ConditionalBreakpoint? breakpoint,
        out bool unknownChannel)
    {
        breakpoint = null;
        unknownChannel = false;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            if (!entry.Imported.Channels.Any(channel => channel.Id == channelId))
            {
                unknownChannel = true;
                return true;
            }

            breakpoint = new ConditionalBreakpoint(
                Guid.NewGuid(),
                channelId,
                comparisonOperator,
                threshold);

            entry.Breakpoints.Add(breakpoint);

            return true;
        }
    }

    public bool TryGetBreakpoints(
        Guid sessionId,
        out IReadOnlyList<ConditionalBreakpoint>? breakpoints)
    {
        breakpoints = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            breakpoints = entry.Breakpoints.GetActive();

            return true;
        }
    }

    public bool TryRemoveBreakpoint(
        Guid sessionId,
        Guid breakpointId,
        out bool removed)
    {
        removed = false;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            removed = breakpointId != Guid.Empty
                      && entry.Breakpoints.Remove(breakpointId);

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

    public bool TrySeekWithSnapshot(
        Guid sessionId,
        long sequenceNumber,
        out ReplaySessionSnapshot? snapshot,
        out bool invalidPosition)
    {
        snapshot = null;
        invalidPosition = false;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            var count = entry.Imported.Reader.SampleCount;

            if (sequenceNumber < 0 || sequenceNumber >= count)
            {
                invalidPosition = true;
                return true;
            }

            entry.Controller.Seek(sequenceNumber);

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }


    public bool TryResetWithSnapshot(
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

            entry.Controller.Reset();

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }


    private sealed class SessionEntry
    {
        public ReplayExecutionController Controller { get; }

        public ImportedCsvRecording Imported { get; }

        public BreakpointCollection Breakpoints { get; } = new();

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
