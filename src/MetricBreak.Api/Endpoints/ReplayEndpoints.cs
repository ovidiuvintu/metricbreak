
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Replay;

namespace MetricBreak.Api.Endpoints;

internal static class ReplayEndpoints
{
    private const string _replayTag = "Replay Controls";
    public static IEndpointRouteBuilder MapReplayEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/sessions/{sessionId:guid}")
            .WithTags(_replayTag);

        // STEP FORWARD
        group.MapPost(
            "/step-forward",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                if (!manager.TryStepForwardWithSnapshot(
                        sessionId,
                        out var advanced,
                        out var snapshot) ||
                    snapshot is null)
                {
                    return Results.NotFound();
                }

                var response = new StepForwardResponse(
                    Advanced: advanced,
                    Session: SessionStateResponseMapper.Map(snapshot));

                return Results.Ok(response);
            })
            .WithName("StepForwardReplaySession");

        // STEP BACKWARD
        group.MapPost(
            "/step-backward",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                if (!manager.TryStepBackwardWithSnapshot(
                        sessionId,
                        out var movedBackward,
                        out var snapshot) ||
                    snapshot is null)
                {
                    return Results.NotFound();
                }

                var response = new StepBackwardResponse(
                    MovedBackward: movedBackward,
                    Session: SessionStateResponseMapper.Map(snapshot));

                return Results.Ok(response);
            })
            .WithName("StepBackwardReplaySession");

        // SEEK
        group.MapPost(
            "/seek",
            (
                Guid sessionId,
                SeekRequest request,
                ReplaySessionManager manager) =>
            {
                var found = manager.TrySeekWithSnapshot(
                    sessionId,
                    request.SequenceNumber,
                    out var snapshot,
                    out var invalidPosition);

                if (!found)
                    return Results.NotFound();

                if (invalidPosition)
                {
                    return Results.BadRequest(new
                    {
                        Error = "InvalidSequenceNumber",
                        Message = "Sequence number is outside the recording."
                    });
                }

                if (snapshot is null)
                {
                    return Results.Problem(
                        "Seek did not produce a snapshot.");
                }

                return Results.Ok(
                    new SeekResponse(
                        SessionStateResponseMapper.Map(snapshot)));
            })
            .WithName("SeekReplaySession");

        // RESET
        group.MapPost(
            "/reset",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                var found = manager.TryResetWithSnapshot(
                    sessionId,
                    out var snapshot);

                if (!found)
                    return Results.NotFound();

                if (snapshot is null)
                {
                    return Results.Problem(
                        "Reset did not produce a snapshot.");
                }

                return Results.Ok(
                    new ResetResponse(
                        SessionStateResponseMapper.Map(snapshot)));
            })
            .WithName("ResetReplaySession");

        // CONTINUE FORWARD
        group.MapPost(
            "/continue",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                var found = manager.TryContinueWithSnapshot(
                    sessionId,
                    out var hadActiveBreakpoints,
                    out var stop,
                    out var snapshot);

                if (!found || snapshot is null)
                    return Results.NotFound();

                var session =
                    SessionStateResponseMapper.Map(snapshot);

                if (!hadActiveBreakpoints)
                {
                    return Results.Ok(new ContinueResponse(
                        Executed: false,
                        HitBreakpoint: false,
                        ReachedEndOfRecording: false,
                        Stop: null,
                        Session: session));
                }

                var hitBreakpoint = stop is not null;

                var reachedEndOfRecording =
                    stop is null &&
                    snapshot.ExecutionState.PauseReason ==
                    ReplayPauseReason.EndOfRecording;

                return Results.Ok(new ContinueResponse(
                    Executed: true,
                    HitBreakpoint: hitBreakpoint,
                    ReachedEndOfRecording: reachedEndOfRecording,
                    Stop: stop is null
                        ? null
                        : ContinueResponseMapper.Map(stop),
                    Session: session));
            })
            .WithName("ContinueReplaySession")
            .Produces<ContinueResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // CONTINUE BACKWARD
        group.MapPost(
            "/continue-backward",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                var found =
                    manager.TryContinueBackwardWithSnapshot(
                        sessionId,
                        out var hadActiveBreakpoints,
                        out var stop,
                        out var snapshot);

                if (!found || snapshot is null)
                    return Results.NotFound();

                var session =
                    SessionStateResponseMapper.Map(snapshot);

                if (!hadActiveBreakpoints)
                {
                    return Results.Ok(new ContinueResponse(
                        Executed: false,
                        HitBreakpoint: false,
                        ReachedEndOfRecording: false,
                        Stop: null,
                        Session: session,
                        ReachedStartOfRecording: false));
                }

                var hitBreakpoint = stop is not null;

                var reachedStartOfRecording =
                    stop is null &&
                    snapshot.ExecutionState.PauseReason ==
                    ReplayPauseReason.StartOfRecording;

                return Results.Ok(new ContinueResponse(
                    Executed: true,
                    HitBreakpoint: hitBreakpoint,
                    ReachedEndOfRecording: false,
                    Stop: stop is null
                        ? null
                        : ContinueResponseMapper.Map(stop),
                    Session: session,
                    ReachedStartOfRecording: reachedStartOfRecording));
            })
            .WithName("ContinueReplaySessionBackward")
            .Produces<ContinueResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
