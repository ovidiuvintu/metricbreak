
using MetricBreak.Api.Breakpoints;
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Breakpoints;

namespace MetricBreak.Api.Endpoints;

internal static class BreakpointEndpoints
{
    private const string _breakpointTag = "Breakpoints";

    public static WebApplication MapBreakpointEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/sessions/{sessionId:guid}/breakpoints",
            (
                Guid sessionId,
                CreateBreakpointRequest request,
                ReplaySessionManager manager) =>
            {
                if (!Enum.TryParse<ComparisonOperator>(
                        request.Operator,
                        ignoreCase: true,
                        out var comparisonOperator))
                {
                    return Results.BadRequest(new
                    {
                        Error = "InvalidComparisonOperator",
                        Message = "Comparison operator is invalid."
                    });
                }

                try
                {
                    var found = manager.TryAddBreakpoint(
                        sessionId,
                        request.ChannelId,
                        comparisonOperator,
                        request.Threshold,
                        out var breakpoint,
                        out var unknownChannel);

                    if (!found)
                        return Results.NotFound();

                    if (unknownChannel)
                    {
                        return Results.BadRequest(new
                        {
                            Error = "UnknownChannelId",
                            Message = "Channel ID is not part of this recording."
                        });
                    }

                    if (breakpoint is null)
                        return Results.Problem(
                            "Breakpoint was not created.");

                    var response =
                        BreakpointResponseMapper.Map(breakpoint);

                    return Results.Created(
                        $"/api/sessions/{sessionId}/breakpoints/{response.BreakpointId}",
                        response);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    return Results.BadRequest(new
                    {
                        Error = exception.Message
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new
                    {
                        Error = exception.Message
                    });
                }
            })
            .WithName("CreateSessionBreakpoint")
            .WithTags(_breakpointTag)
            .Produces<BreakpointResponse>(
                StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
            "/api/sessions/{sessionId:guid}/breakpoints",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                var found = manager.TryGetBreakpoints(
                    sessionId,
                    out var breakpoints);

                if (!found || breakpoints is null)
                    return Results.NotFound();

                var response = new BreakpointsResponse(
                    breakpoints
                        .Select(BreakpointResponseMapper.Map)
                        .ToArray());

                return Results.Ok(response);
            })
            .WithName("GetSessionBreakpoints")
            .WithTags(_breakpointTag)
            .Produces<BreakpointsResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete(
            "/api/sessions/{sessionId:guid}/breakpoints/{breakpointId:guid}",
            (
                Guid sessionId,
                Guid breakpointId,
                ReplaySessionManager manager) =>
            {
                var found = manager.TryRemoveBreakpoint(
                    sessionId,
                    breakpointId,
                    out var removed);

                if (!found || !removed)
                    return Results.NotFound();

                return Results.NoContent();
            })
            .WithName("DeleteSessionBreakpoint")
            .WithTags(_breakpointTag)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPatch(
            "/api/sessions/{sessionId:guid}/breakpoints/{breakpointId:guid}",
            (
                Guid sessionId,
                Guid breakpointId,
                SetBreakpointEnabledRequest request,
                ReplaySessionManager manager) =>
            {
                var found = manager.TrySetBreakpointEnabled(
                    sessionId,
                    breakpointId,
                    request.IsEnabled,
                    out var updated);

                if (!found || !updated)
                    return Results.NotFound();

                return Results.NoContent();
            })
            .WithName("SetSessionBreakpointEnabled")
            .WithTags(_breakpointTag)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

