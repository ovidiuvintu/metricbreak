
using Microsoft.AspNetCore.Mvc;
using MetricBreak.Api.Sessions;

namespace MetricBreak.Api.Endpoints;

internal static class SessionEndpoints
{
    private const int _maxSampleCount = 5000;

    private const string _sessionTag = "Sessions";

    public static IEndpointRouteBuilder MapSessionEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/sessions")
            .WithTags(_sessionTag);

        // CREATE SESSION FROM CSV
        group.MapPost(
            "",
            async (
                IFormFile file,
                CsvSessionImportService importService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var response = await importService.ImportAsync(
                        file,
                        cancellationToken);

                    return Results.Created(
                        $"/api/sessions/{response.SessionId}",
                        response);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new
                    {
                        Error = exception.Message
                    });
                }
                catch (FormatException exception)
                {
                    return Results.BadRequest(new
                    {
                        Error = exception.Message
                    });
                }
                catch (InvalidDataException exception)
                {
                    return Results.BadRequest(new
                    {
                        Error = exception.Message
                    });
                }
            })
            .DisableAntiforgery()
            .WithName("CreateReplaySession")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CreateSessionResponse>(
                StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        // GET SESSION STATE
        group.MapGet(
            "/{sessionId:guid}",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                if (!manager.TryGetSnapshot(
                        sessionId,
                        out var snapshot) ||
                    snapshot is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(
                    SessionStateResponseMapper.Map(snapshot));
            })
            .WithName("GetReplaySessionState")
            .Produces<SessionStateResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // GET READ-ONLY SAMPLE RANGE
        group.MapGet(
            "/{sessionId:guid}/samples",
            (
                [FromRoute] Guid sessionId,
                [FromQuery(Name = "start")] long start,
                [FromQuery(Name = "count")] int count,
                ReplaySessionManager manager) =>
            {
                if (start < 0)
                {
                    return Results.BadRequest(new
                    {
                        Error = "Start must be non-negative."
                    });
                }

                if (count < 1 || count > _maxSampleCount)
                {
                    return Results.BadRequest(new
                    {
                        Error =
                            $"Count must be between 1 and {_maxSampleCount}."
                    });
                }

                if (!manager.TryGetSampleRange(
                        sessionId,
                        start,
                        count,
                        out var response))
                {
                    return Results.NotFound(new
                    {
                        Error = "Replay session not found."
                    });
                }

                return Results.Ok(response);
            })
            .WithName("GetSessionSamples")     
            .Produces<SampleRangeResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
