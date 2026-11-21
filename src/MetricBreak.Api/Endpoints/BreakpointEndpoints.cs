
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
                    var found = manager.TryAddTypedBreakpoint(
                        sessionId,
                        request.ChannelId,
                        comparisonOperator,
                        request.Threshold,
                        request.Value,
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

                    var response = BreakpointResponseMapper.Map(
                        breakpoint,
                        isEnabled: true);

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

        app.MapPost(
            "/api/sessions/{sessionId:guid}/breakpoints/composite",
            (
                Guid sessionId,
                CreateCompositeBreakpointRequest request,
                ReplaySessionManager manager) =>
            {
                if (!Enum.TryParse<LogicalOperator>(
                        request.LogicalOperator,
                        ignoreCase: true,
                        out var logicalOperator))
                {
                    return Results.BadRequest(new
                    {
                        Error = "InvalidLogicalOperator",
                        Message = "Logical operator is invalid. Use And or Or."
                    });
                }

                if (request.Conditions is null || request.Conditions.Count < 2)
                {
                    return Results.BadRequest(new
                    {
                        Error = "InvalidCompositeConditions",
                        Message = "Composite breakpoints require at least two conditions."
                    });
                }

                var parsedConditions = new List<BreakpointCondition>(request.Conditions.Count);

                foreach (var condition in request.Conditions)
                {
                    if (!Enum.TryParse<ComparisonOperator>(
                            condition.Operator,
                            ignoreCase: true,
                            out var comparisonOperator) || !Enum.IsDefined(comparisonOperator))
                    {
                        return Results.BadRequest(new
                        {
                            Error = "InvalidComparisonOperator",
                            Message = "Comparison operator is invalid."
                        });
                    }

                    try
                    {
                        parsedConditions.Add(new BreakpointCondition(
                            condition.ChannelId, comparisonOperator, condition.Threshold,
                            manager.ParseConditionValue(sessionId, condition.ChannelId, comparisonOperator, condition.Value)));
                    }
                    catch (ArgumentException exception)
                    {
                        return Results.BadRequest(new { Error = exception.Message });
                    }
                }

                try
                {
                    var found = manager.TryAddCompositeBreakpoint(
                        sessionId,
                        logicalOperator,
                        parsedConditions,
                        out var breakpoint,
                        out var unknownChannel);

                    if (!found)
                        return Results.NotFound();

                    if (unknownChannel)
                    {
                        return Results.BadRequest(new
                        {
                            Error = "UnknownChannelId",
                            Message = "One or more channel IDs are not part of this recording."
                        });
                    }

                    if (breakpoint is null)
                        return Results.Problem("Composite breakpoint was not created.");

                    var response = new CompositeBreakpointResponse(
                        BreakpointId: breakpoint.Id,
                        LogicalOperator: breakpoint.LogicalOperator.ToString(),
                        Conditions: breakpoint.Conditions
                            .Select(condition => new BreakpointConditionRequest(
                                condition.ChannelId,
                                condition.Operator.ToString(),
                                condition.Threshold,
                                BreakpointValueParser.ToWire(condition.ExpectedValue)))
                            .ToArray(),
                        IsEnabled: true);

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
            .WithName("CreateSessionCompositeBreakpoint")
            .WithTags(_breakpointTag)
            .Produces<CompositeBreakpointResponse>(
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

                var foundComposites = manager.TryGetCompositeBreakpoints(
                    sessionId,
                    out var compositeBreakpoints);

                if (!found || !foundComposites || breakpoints is null || compositeBreakpoints is null)
                    return Results.NotFound();

                var response = new BreakpointsResponse(
                    breakpoints
                        .Select(registered =>
                            BreakpointResponseMapper.Map(
                                registered.Breakpoint,
                                registered.IsEnabled))
                        .ToArray(),
                    compositeBreakpoints
                        .Select(registered => new CompositeBreakpointResponse(
                            BreakpointId: registered.Breakpoint.Id,
                            LogicalOperator: registered.Breakpoint.LogicalOperator.ToString(),
                            Conditions: registered.Breakpoint.Conditions
                                .Select(condition => new BreakpointConditionRequest(
                                    condition.ChannelId,
                                    condition.Operator.ToString(),
                                    condition.Threshold,
                                    BreakpointValueParser.ToWire(condition.ExpectedValue)))
                                .ToArray(),
                            IsEnabled: registered.IsEnabled))
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

