using System.Collections.Concurrent;
using MetricBreak.Api.Investigations;
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

    public bool TryExportInvestigation(
        Guid sessionId,
        out ExportedInvestigation? investigation)
    {
        investigation = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            var snapshot = CreateSnapshot(sessionId, entry);

            var channelNames = entry.Imported.Channels
                .ToDictionary(
                    channel => channel.Id,
                    channel => channel.Name);

            var breakpoints = entry.Breakpoints
                .GetRegistered()
                .Select(registered =>
                {
                    var breakpoint = registered.Breakpoint;

                    channelNames.TryGetValue(
                        breakpoint.ChannelId,
                        out var channelName);

                    return new BreakpointExportSnapshot(
                        BreakpointId: breakpoint.Id,
                        ChannelName: channelName ?? breakpoint.ChannelId.ToString(),
                        Operator: breakpoint.Operator.ToString(),
                        Threshold: breakpoint.Threshold,
                        IsEnabled: registered.IsEnabled);
                })
                .ToArray();

            investigation = new ExportedInvestigation(
                SchemaVersion: 1,
                InvestigationId: Guid.NewGuid(),
                ExportedAtUtc: DateTimeOffset.UtcNow,
                Recording: new RecordingExportSnapshot(
                    SourceName: entry.Imported.Recording.Source.Name,
                    SourceContentHash: entry.Imported.Recording.Source.ContentHash,
                    SourceSizeInBytes: entry.Imported.Recording.Source.SizeInBytes,
                    RecordingName: entry.Imported.Recording.Name,
                    SampleCount: entry.Imported.Reader.SampleCount),
                Session: new SessionExportSnapshot(
                    SequenceNumber: snapshot.Position.SequenceNumber,
                    ExecutionState:
                        snapshot.ExecutionState.Status.ToString(),
                    PauseReason:
                        snapshot.ExecutionState.PauseReason.ToString()),
                Breakpoints: breakpoints);

            return true;
        }
    }

    public bool TryImportInvestigation(
        ImportedCsvRecording imported,
        ExportedInvestigation investigation,
        out Guid sessionId,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentNullException.ThrowIfNull(investigation);

        sessionId = Guid.Empty;
        error = null;

        if (imported.Recording.Source.ContentHash !=
            investigation.Recording.SourceContentHash)
        {
            error = "Uploaded recording content does not match the exported investigation.";
            return false;
        }

        if (imported.Recording.Source.SizeInBytes !=
            investigation.Recording.SourceSizeInBytes)
        {
            error = "Uploaded recording size does not match the exported investigation.";
            return false;
        }

        if (investigation.Session.SequenceNumber < 0 ||
            investigation.Session.SequenceNumber >= imported.Reader.SampleCount)
        {
            error = "Exported sequence number is outside the uploaded recording.";
            return false;
        }

        var channelByName = imported.Channels
            .ToDictionary(
                channel => channel.Name,
                channel => channel,
                StringComparer.Ordinal);

        var session = new ReplaySession(
            imported.Recording,
            imported.Reader);

        var controller = new ReplayExecutionController(session);

        if (investigation.Session.SequenceNumber != 0)
        {
            controller.Seek(investigation.Session.SequenceNumber);
        }

        var entry = new SessionEntry(controller, imported);

        foreach (var exportedBreakpoint in investigation.Breakpoints)
        {
            if (!channelByName.TryGetValue(
                    exportedBreakpoint.ChannelName,
                    out var channel))
            {
                error = $"Channel '{exportedBreakpoint.ChannelName}' was not found in the uploaded recording.";
                return false;
            }

            if (!Enum.TryParse<ComparisonOperator>(
                    exportedBreakpoint.Operator,
                    ignoreCase: true,
                    out var comparisonOperator))
            {
                error = $"Unsupported comparison operator '{exportedBreakpoint.Operator}'.";
                return false;
            }

            var breakpoint = new ConditionalBreakpoint(
                exportedBreakpoint.BreakpointId,
                channel.Id,
                comparisonOperator,
                exportedBreakpoint.Threshold);

            entry.Breakpoints.Add(breakpoint);

            if (!exportedBreakpoint.IsEnabled)
            {
                entry.Breakpoints.Disable(breakpoint.Id);
            }
        }

        sessionId = Guid.NewGuid();

        if (!_sessions.TryAdd(sessionId, entry))
        {
            error = "Unable to create replay session from investigation.";
            sessionId = Guid.Empty;
            return false;
        }

        return true;
    }

    public bool TryContinueBackwardWithSnapshot(
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
                stop = entry.Controller.ContinueBackward(entry.Breakpoints);
            }

            snapshot = CreateSnapshot(sessionId, entry);

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


    public bool TryGetChannels(
        Guid sessionId,
        out IReadOnlyList<ChannelMetadataResponse>? channels)
    {
        channels = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            channels = entry.Imported.Channels
                .Select(channel => new ChannelMetadataResponse(
                    ChannelId: channel.Id,
                    Name: channel.Name,
                    Unit: channel.Unit))
                .ToArray();

            return true;
        }
    }

}
