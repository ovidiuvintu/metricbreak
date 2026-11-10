
using MetricBreak.Api.Sessions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ReplaySessionManager>();
builder.Services.AddScoped<CsvSessionImportService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost(
    "/api/sessions",
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
    .WithTags("Sessions")
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<CreateSessionResponse>(
        StatusCodes.Status201Created)
    .Produces(
        StatusCodes.Status400BadRequest);

app.MapGet(
    "/api/sessions/{sessionId:guid}",
    (
        Guid sessionId,
        ReplaySessionManager manager) =>
    {
        if (!manager.TryGetSnapshot(sessionId, out var snapshot)
            || snapshot is null)
        {
            return Results.NotFound();
        }

        // Compiler now knows snapshot is non-null.
        var sample = snapshot.CurrentSample;

        var response = new SessionStateResponse(
            SessionId: snapshot.SessionId,
            RecordingId: snapshot.RecordingId,
            RecordingName: snapshot.RecordingName,
            SampleCount: snapshot.SampleCount,
            ExecutionState:
                snapshot.ExecutionState.Status.ToString(),
            PauseReason:
                snapshot.ExecutionState.PauseReason.ToString(),
            CurrentSample: new CurrentSampleResponse(
                Sequence: sample.SequenceNumber,
                TimestampNanoseconds:
                    sample.Timestamp.UnixNanoseconds,
                Channels: sample.Values));

        return Results.Ok(
            SessionStateResponseMapper.Map(snapshot));
    });

app.MapPost(
    "/api/sessions/{sessionId:guid}/step-forward",
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
    .WithName("StepForwardReplaySession")
    .WithTags("Replay Sessions");

app.MapPost(
    "/api/sessions/{sessionId:guid}/step-backward",
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
    .WithName("StepBackwardReplaySession")
    .WithTags("Replay Sessions");

app.MapPost(
    "/api/sessions/{sessionId:guid}/seek",
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
            return Results.Problem("Seek did not produce a snapshot.");

        return Results.Ok(new SeekResponse(
            SessionStateResponseMapper.Map(snapshot)));
    })
    .WithName("SeekReplaySession")
    .WithTags("Replay Sessions");


app.MapPost(
    "/api/sessions/{sessionId:guid}/reset",
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
            return Results.Problem(
                "Reset did not produce a snapshot.");

        return Results.Ok(
            new ResetResponse(
                SessionStateResponseMapper.Map(snapshot)));
    })
    .WithName("ResetReplaySession")
    .WithTags("Replay Sessions");


app.Run();

public partial class Program { }

