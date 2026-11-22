using System.Collections.Concurrent;
using MetricBreak.Api.Breakpoints;
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

    public bool TryGetTimeline(
        Guid sessionId,
        out RecordingTimelineResponse? timeline)
    {
        timeline = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            var snapshot = RecordingTimelineMapping.ToSnapshot(entry.Imported.Timeline);
            timeline = new RecordingTimelineResponse(
                snapshot.Events,
                snapshot.Stages);
            return true;
        }
    }

    public bool TryExportInvestigation(
        Guid sessionId,
        ExportInvestigationRequest? request,
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
                .GetAllRegistered()
                .Select(registered => registered.Breakpoint switch
                {
                    ConditionalBreakpoint breakpoint => new BreakpointExportSnapshot(
                        BreakpointId: breakpoint.Id,
                        ChannelName: channelNames.TryGetValue(breakpoint.ChannelId, out var channelName)
                            ? channelName
                            : breakpoint.ChannelId.ToString(),
                        Operator: breakpoint.Operator.ToString(),
                        Threshold: breakpoint.Threshold,
                        IsEnabled: registered.IsEnabled),
                    MissingDataBreakpoint missing => new BreakpointExportSnapshot(
                        BreakpointId: missing.Id,
                        ChannelName: missing.ChannelId is Guid channelId &&
                                     channelNames.TryGetValue(channelId, out var missingChannel)
                            ? missingChannel
                            : "",
                        Operator: MissingDataBreakpoint.OperatorName,
                        Threshold: missing.MinimumSeconds,
                        IsEnabled: registered.IsEnabled),
                    _ => null,
                })
                .OfType<BreakpointExportSnapshot>()
                .ToArray();

            var investigationId = request?.InvestigationId is Guid existingId &&
                                    existingId != Guid.Empty
                ? existingId
                : Guid.NewGuid();

            var name = string.IsNullOrWhiteSpace(request?.Name)
                ? entry.Imported.Recording.Name
                : request!.Name!.Trim();

            var metadata = new InvestigationMetadataSnapshot(
                name,
                string.IsNullOrWhiteSpace(request?.Description)
                    ? null
                    : request!.Description!.Trim(),
                request?.ProjectId,
                DateTimeOffset.UtcNow);

            investigation = new ExportedInvestigation(
                SchemaVersion: InvestigationSchema.CurrentVersion,
                InvestigationId: investigationId,
                ExportedAtUtc: DateTimeOffset.UtcNow,
                Recording: new RecordingExportSnapshot(
                    SourceName: entry.Imported.Recording.Source.Name,
                    SourceContentHash: entry.Imported.Recording.Source.ContentHash,
                    SourceSizeInBytes: entry.Imported.Recording.Source.SizeInBytes,
                    RecordingName: entry.Imported.Recording.Name,
                    SampleCount: entry.Imported.Reader.SampleCount,
                    RecordingId: entry.Imported.Recording.Id),
                Session: new SessionExportSnapshot(
                    SequenceNumber: snapshot.Position.SequenceNumber,
                    ExecutionState:
                        snapshot.ExecutionState.Status.ToString(),
                    PauseReason:
                        snapshot.ExecutionState.PauseReason.ToString()),
                Breakpoints: breakpoints,
                Bookmarks: Array.Empty<InvestigationBookmarkSnapshot>(),
                Metadata: metadata,
                UiState: request?.UiState,
                Timeline: RecordingTimelineMapping.ToSnapshot(entry.Imported.Timeline));

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
            if (string.Equals(
                    exportedBreakpoint.Operator,
                    MissingDataBreakpoint.OperatorName,
                    StringComparison.OrdinalIgnoreCase))
            {
                Guid? missingChannelId = null;
                if (!string.IsNullOrWhiteSpace(exportedBreakpoint.ChannelName))
                {
                    if (!channelByName.TryGetValue(
                            exportedBreakpoint.ChannelName,
                            out var missingChannel))
                    {
                        error = $"Channel '{exportedBreakpoint.ChannelName}' was not found in the uploaded recording.";
                        return false;
                    }

                    missingChannelId = missingChannel.Id;
                }

                MissingDataBreakpoint missingBreakpoint;
                try
                {
                    missingBreakpoint = new MissingDataBreakpoint(
                        exportedBreakpoint.BreakpointId,
                        exportedBreakpoint.Threshold,
                        missingChannelId);
                }
                catch (ArgumentException exception)
                {
                    error = exception.Message;
                    return false;
                }

                entry.Breakpoints.Add(missingBreakpoint);

                if (!exportedBreakpoint.IsEnabled)
                    entry.Breakpoints.Disable(missingBreakpoint.Id);

                continue;
            }

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

            try
            {
                BreakpointOperatorValidation.Validate(channel.ValueType, comparisonOperator);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
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

    // Must be called while holding entry.SyncRoot.
    private static void InvalidatePlayback(SessionEntry entry)
    {
        entry.PlaybackActive = false;
        entry.PlaybackGeneration = checked(entry.PlaybackGeneration + 1);
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

            InvalidatePlayback(entry);
            hadActiveBreakpoints = entry.Breakpoints.ActiveCount > 0;

            if (hadActiveBreakpoints)
            {
                stop = entry.Controller.ContinueBackward(entry.Breakpoints);
            }

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }

    public bool TryRunToBreakpointWithSnapshot(
        Guid sessionId,
        Guid breakpointId,
        bool forward,
        out BreakpointStop? stop,
        out ReplaySessionSnapshot? snapshot,
        out bool unknownBreakpoint)
    {
        stop = null;
        snapshot = null;
        unknownBreakpoint = false;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            if (!entry.Breakpoints.TryGetBreakpoint(breakpointId, out var breakpoint) ||
                breakpoint is null)
            {
                unknownBreakpoint = true;
                snapshot = CreateSnapshot(sessionId, entry);
                return true;
            }

            InvalidatePlayback(entry);
            stop = entry.Controller.RunToBreakpoint(breakpoint, forward);
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

            InvalidatePlayback(entry);
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

            var channel = entry.Imported.Channels.Single(c => c.Id == channelId);
            BreakpointOperatorValidation.Validate(channel.ValueType, comparisonOperator);

            breakpoint = new ConditionalBreakpoint(
                Guid.NewGuid(),
                channelId,
                comparisonOperator,
                threshold);

            entry.Breakpoints.Add(breakpoint);

            return true;
        }
    }

    public bool TryAddTypedBreakpoint(Guid sessionId, Guid channelId,
        ComparisonOperator op, double threshold, string? value,
        out ConditionalBreakpoint? breakpoint, out bool unknownChannel)
    {
        breakpoint = null;
        unknownChannel = false;
        if (!_sessions.TryGetValue(sessionId, out var entry)) return false;
        lock (entry.SyncRoot)
        {
            if (entry.IsClosed) return false;
            var channel = entry.Imported.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is null) { unknownChannel = true; return true; }
            BreakpointOperatorValidation.Validate(channel.ValueType, op);
            var expected = ParseExpected(channel.ValueType, op, value);
            breakpoint = new ConditionalBreakpoint(Guid.NewGuid(), channelId, op, threshold, expected);
            entry.Breakpoints.Add(breakpoint);
            return true;
        }
    }

    public bool TryAddMissingDataBreakpoint(
        Guid sessionId,
        double minimumSeconds,
        Guid? channelId,
        out MissingDataBreakpoint? breakpoint,
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

            Guid? resolvedChannel = null;
            if (channelId is Guid id && id != Guid.Empty)
            {
                if (entry.Imported.Channels.All(channel => channel.Id != id))
                {
                    unknownChannel = true;
                    return true;
                }

                resolvedChannel = id;
            }

            breakpoint = new MissingDataBreakpoint(
                Guid.NewGuid(),
                minimumSeconds,
                resolvedChannel);
            entry.Breakpoints.Add(breakpoint);
            return true;
        }
    }

    private static MetricBreak.Domain.Recordings.ChannelValue? ParseExpected(
        MetricBreak.Domain.Recordings.ChannelValueType type, ComparisonOperator op, string? value)
    {
        if (op is not (ComparisonOperator.Equal or ComparisonOperator.NotEqual)) return null;
        if (type == MetricBreak.Domain.Recordings.ChannelValueType.Numeric) return null;
        if (value is null) throw new ArgumentException("A typed comparison value is required.");
        if (type == MetricBreak.Domain.Recordings.ChannelValueType.Boolean)
        {
            if (!bool.TryParse(value, out var parsed)) throw new ArgumentException("Boolean value must be true or false.");
            return new MetricBreak.Domain.Recordings.ChannelValue.Boolean(parsed);
        }
        return new MetricBreak.Domain.Recordings.ChannelValue.Categorical(value);
    }

    public MetricBreak.Domain.Recordings.ChannelValue? ParseConditionValue(Guid sessionId,
        Guid channelId, ComparisonOperator op, string? value)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry)) return null;
        lock (entry.SyncRoot)
        {
            var channel = entry.Imported.Channels.FirstOrDefault(c => c.Id == channelId);
            return channel is null ? null : ParseExpected(channel.ValueType, op, value);
        }
    }

    public bool TryGetBreakpointResponses(
        Guid sessionId,
        out IReadOnlyList<BreakpointResponse>? breakpoints)
    {
        breakpoints = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            breakpoints = entry.Breakpoints
                .GetAllRegistered()
                .Select(registered => registered.Breakpoint switch
                {
                    ConditionalBreakpoint conditional =>
                        BreakpointResponseMapper.Map(conditional, registered.IsEnabled),
                    MissingDataBreakpoint missing =>
                        BreakpointResponseMapper.Map(missing, registered.IsEnabled),
                    _ => null,
                })
                .OfType<BreakpointResponse>()
                .ToArray();

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


    public bool TryAddCompositeBreakpoint(
        Guid sessionId,
        LogicalOperator logicalOperator,
        IReadOnlyList<BreakpointCondition> conditions,
        out CompositeBreakpoint? breakpoint,
        out bool unknownChannel)
    {
        breakpoint = null;
        unknownChannel = false;

        ArgumentNullException.ThrowIfNull(conditions);

        if (!Enum.IsDefined(logicalOperator))
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalOperator));
        }

        if (conditions.Count < 2)
        {
            throw new ArgumentException(
                "Composite breakpoints require at least two conditions.",
                nameof(conditions));
        }

        if (conditions.Any(condition => condition is null))
        {
            throw new ArgumentException(
                "Conditions cannot contain null entries.",
                nameof(conditions));
        }

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            var channelIds = entry.Imported.Channels
                .Select(channel => channel.Id)
                .ToHashSet();

            if (conditions.Any(condition =>
                    !channelIds.Contains(condition.ChannelId)))
            {
                unknownChannel = true;
                return true;
            }

            foreach (var condition in conditions)
            {
                var channel = entry.Imported.Channels.Single(c => c.Id == condition.ChannelId);
                BreakpointOperatorValidation.Validate(channel.ValueType, condition.Operator);
                if (condition.Operator is ComparisonOperator.Equal or ComparisonOperator.NotEqual &&
                    channel.ValueType != MetricBreak.Domain.Recordings.ChannelValueType.Numeric &&
                    (condition.ExpectedValue is null ||
                     channel.ValueType == MetricBreak.Domain.Recordings.ChannelValueType.Boolean && condition.ExpectedValue is not MetricBreak.Domain.Recordings.ChannelValue.Boolean ||
                     channel.ValueType == MetricBreak.Domain.Recordings.ChannelValueType.Categorical && condition.ExpectedValue is not MetricBreak.Domain.Recordings.ChannelValue.Categorical))
                    throw new ArgumentException("Condition value must match the channel type.");
            }

            // Validate all conditions before modifying the collection.
            var candidate = new CompositeBreakpoint(
                Guid.NewGuid(),
                logicalOperator,
                conditions);

            entry.Breakpoints.Add(candidate);

            breakpoint = candidate;

            return true;
        }
    }

    public bool TryGetCompositeBreakpoints(
        Guid sessionId,
        out IReadOnlyList<(
            CompositeBreakpoint Breakpoint,
            bool IsEnabled)>? breakpoints)
    {
        breakpoints = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            breakpoints = entry.Breakpoints
                .GetAllRegistered()
                .Where(registered =>
                    registered.Breakpoint is CompositeBreakpoint)
                .Select(registered => (
                    Breakpoint: (CompositeBreakpoint)registered.Breakpoint,
                    IsEnabled: registered.IsEnabled))
                .ToArray();

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
            CurrentSample: controller.CurrentSample,
            TimestampOrigin: imported.TimestampOrigin);
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
        return TryStepForwardWithSnapshot(
            sessionId,
            out advanced,
            out _);
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

            InvalidatePlayback(entry);
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

            InvalidatePlayback(entry);
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

            InvalidatePlayback(entry);
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

            InvalidatePlayback(entry);
            entry.Controller.Reset();

            snapshot = CreateSnapshot(sessionId, entry);

            return true;
        }
    }


    // A new generation is issued for each Play/Resume action.
    public bool TryStartPlayback(Guid sessionId, out long generation)
    {
        generation = 0;
        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            entry.PlaybackGeneration = checked(entry.PlaybackGeneration + 1);
            entry.PlaybackActive = true;
            generation = entry.PlaybackGeneration;
            return true;
        }
    }

    public bool TryAdvancePlaybackWithSnapshot(
        Guid sessionId,
        long generation,
        int maxSamples,
        out bool accepted,
        out PlaybackAdvanceResult? result,
        out ReplaySessionSnapshot? snapshot)
    {
        if (maxSamples < 1 || maxSamples > 5000)
            throw new ArgumentOutOfRangeException(nameof(maxSamples));

        accepted = false;
        result = null;
        snapshot = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            if (!entry.PlaybackActive ||
                generation != entry.PlaybackGeneration)
            {
                snapshot = CreateSnapshot(sessionId, entry);
                return true;
            }

            accepted = true;
            result = entry.Controller.AdvancePlayback(maxSamples, entry.Breakpoints);
            snapshot = CreateSnapshot(sessionId, entry);

            // A completed or breakpoint-stopped batch cannot accept another
            // advance under the same generation.
            if (entry.Controller.State.Status != ReplayExecutionStatus.Running)
            {
                entry.PlaybackActive = false;
                entry.PlaybackGeneration = checked(entry.PlaybackGeneration + 1);
            }

            return true;
        }
    }

    public bool TryPausePlaybackWithSnapshot(
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

            InvalidatePlayback(entry);
            entry.Controller.PausePlayback();
            snapshot = CreateSnapshot(sessionId, entry);
            return true;
        }
    }

    public bool TryListBreakpointHitsInRange(
        Guid sessionId,
        long fromSequenceInclusive,
        long toSequenceExclusive,
        int maxHits,
        out BreakpointHitsResponse? response)
    {
        response = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            if (maxHits <= 0)
                maxHits = 2000;

            const int maxRange = 100_000;
            if (toSequenceExclusive - fromSequenceInclusive > maxRange)
            {
                return false;
            }

            var hits = entry.Controller.FindBreakpointHitsInRange(
                entry.Breakpoints,
                fromSequenceInclusive,
                toSequenceExclusive,
                maxHits);

            response = new BreakpointHitsResponse(
                hits.Select(static hit =>
                    new BreakpointHitOccurrenceResponse(
                        hit.BreakpointId,
                        hit.SampleSequence)).ToArray(),
                hits.Count >= maxHits);

            return true;
        }
    }

    public bool TryMeasureBreakpointCoverage(
        Guid sessionId,
        out BreakpointCoverageResponse? response)
    {
        response = null;

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            response = new BreakpointCoverageResponse(
                entry.Controller.MeasureBreakpointCoverage(entry.Breakpoints)
                    .Select(static coverage => new BreakpointCoverageItemResponse(
                        coverage.BreakpointId,
                        coverage.Crossings,
                        coverage.MatchingSamples,
                        coverage.MatchingNanoseconds,
                        coverage.LongestRunSamples,
                        coverage.LongestRunNanoseconds,
                        coverage.Relation switch
                        {
                            Domain.Breakpoints.BreakpointCoverageRelation.Above => "above",
                            Domain.Breakpoints.BreakpointCoverageRelation.AtOrAbove => "atOrAbove",
                            Domain.Breakpoints.BreakpointCoverageRelation.Below => "below",
                            Domain.Breakpoints.BreakpointCoverageRelation.AtOrBelow => "atOrBelow",
                            _ => "matching",
                        }))
                    .ToArray());

            return true;
        }
    }

    public bool TryStopPlaybackWithSnapshot(
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

            InvalidatePlayback(entry);
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

        public long PlaybackGeneration { get; set; }

        public bool PlaybackActive { get; set; }

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
                    Unit: channel.Unit)
                {
                    ValueType = channel.ValueType.ToString()
                })
                .ToArray();

            return true;
        }
    }
    public bool TryNavigateChangeWithSnapshot(
        Guid sessionId,
        Guid channelId,
        bool forward,
        out bool unknownChannel,
        out bool foundChange,
        out ReplaySessionSnapshot? snapshot)
    {
        unknownChannel = false;
        foundChange = false;
        snapshot = null;
        if (!_sessions.TryGetValue(sessionId, out var entry)) return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed) return false;
            if (channelId == Guid.Empty || !entry.Imported.Channels.Any(c => c.Id == channelId))
            {
                unknownChannel = true;
                return true;
            }

            InvalidatePlayback(entry);
            var position = entry.Controller.Position.SequenceNumber;
            var target = TelemetryChangeNavigator.Find(entry.Imported.Reader, channelId, position, forward);
            if (target.HasValue)
            {
                entry.Controller.Seek(target.Value);
                foundChange = true;
            }
            snapshot = CreateSnapshot(sessionId, entry);
            return true;
        }
    }

    public bool TryGetSampleRange(
        Guid sessionId,
        long start,
        int count,
        out SampleRangeResponse? response)
    {
        const int maxCount = 5000;

        response = null;

        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));

        if (count < 1 || count > maxCount)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (!_sessions.TryGetValue(sessionId, out var entry))
            return false;

        lock (entry.SyncRoot)
        {
            if (entry.IsClosed)
                return false;

            var reader = entry.Imported.Reader;
            var totalSamples = reader.SampleCount;

            var available = start >= totalSamples
                ? 0L
                : Math.Min((long)count, totalSamples - start);

            var samples = new List<SampleRangeItemResponse>(
                checked((int)available));

            for (long offset = 0; offset < available; offset++)
            {
                var sample = reader.Read(start + offset);

                samples.Add(new SampleRangeItemResponse(
                    Sequence: sample.SequenceNumber,
                    TimestampNanoseconds:
                        sample.Timestamp.UnixNanoseconds.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                    Channels: sample.Values.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value))
                {
                    TypedChannels = TypedChannelResponseMapper.Map(sample)
                });
            }

            response = new SampleRangeResponse(
                SessionId: sessionId,
                Start: start,
                Count: samples.Count,
                TotalSamples: totalSamples,
                Samples: samples);

            return true;
        }
    }
}
