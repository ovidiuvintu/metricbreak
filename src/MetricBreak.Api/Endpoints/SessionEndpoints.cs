
using Microsoft.AspNetCore.Mvc;
using MetricBreak.Api.Analysis;
using MetricBreak.Api.Recordings;
using MetricBreak.Api.Sessions;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Endpoints;

internal static class SessionEndpoints
{
    private sealed record ChangeNavigationRequest(Guid ChannelId);

    private static IResult NavigateChange(
        Guid sessionId, ChangeNavigationRequest request, ReplaySessionManager manager, bool forward)
    {
        if (!manager.TryNavigateChangeWithSnapshot(sessionId, request.ChannelId, forward,
                out var unknownChannel, out var foundChange, out var snapshot))
            return Results.NotFound(new { Error = "Replay session not found." });
        if (unknownChannel)
            return Results.BadRequest(new { Error = "Unknown channel ID." });
        return Results.Ok(new
        {
            FoundChange = foundChange,
            Session = SessionStateResponseMapper.Map(snapshot!)
        });
    }

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
                [FromForm] int? timestampColumn,
                [FromForm] string? timestampFormat,
                CsvSessionImportService importService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mapping = TimestampMappingReader.Read(
                        timestampColumn,
                        timestampFormat);

                    var response = await importService.ImportAsync(
                        file,
                        mapping,
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

        group.MapPost(
            "/timestamp-preview",
            async (
                IFormFile file,
                [FromForm] int? timestampColumn,
                [FromForm] string? timestampFormat,
                CsvSessionImportService importService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mapping = TimestampMappingReader.Read(
                        timestampColumn,
                        timestampFormat);

                    var preview = await importService.PreviewAsync(
                        file,
                        mapping,
                        cancellationToken);

                    return Results.Ok(preview);
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
            .WithName("PreviewCsvTimestampMapping")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<TimestampPreviewResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete(
            "/{sessionId:guid}",
            (Guid sessionId, ReplaySessionManager manager) =>
                manager.Remove(sessionId)
                    ? Results.NoContent()
                    : Results.NotFound())
            .WithName("CloseReplaySession")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(
            "/{sessionId:guid}/timeline",
            (Guid sessionId, ReplaySessionManager manager) =>
            {
                if (!manager.TryGetTimeline(sessionId, out var timeline) ||
                    timeline is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(timeline);
            })
            .WithName("GetSessionRecordingTimeline")
            .Produces<RecordingTimelineResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

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

        group.MapGet(
            "/{sessionId:guid}/analysis",
            (
                Guid sessionId,
                [FromQuery] long? start,
                [FromQuery] long? end,
                [FromQuery] Guid[]? channelId,
                [FromQuery] long? cadenceNanoseconds,
                ReplaySessionManager manager) =>
            {
                if (!manager.TryAnalyzeWindow(
                        sessionId,
                        start,
                        end,
                        channelId,
                        cadenceNanoseconds,
                        out var analysis,
                        out var error))
                {
                    return error is null
                        ? Results.NotFound(new { Error = "Replay session not found." })
                        : Results.BadRequest(new { Error = error });
                }

                return Results.Ok(analysis);
            })
            .WithName("AnalyzeSessionWindow")
            .Produces<WindowAnalysisResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(
            "/{sessionId:guid}/analysis/event",
            (
                Guid sessionId,
                [FromQuery] long? sequence,
                [FromQuery] int? lookback,
                ReplaySessionManager manager) =>
            {
                if (!manager.TryAnalyzeEvent(
                        sessionId,
                        sequence,
                        lookback ?? 10,
                        out var analysis,
                        out var error))
                {
                    return error is null
                        ? Results.NotFound(new { Error = "Replay session not found." })
                        : Results.BadRequest(new { Error = error });
                }

                return Results.Ok(analysis);
            })
            .WithName("AnalyzeSessionEvent")
            .Produces<EventAnalysisResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(
            "/{sessionId:guid}/analysis/compare",
            (
                Guid sessionId,
                [FromQuery] Guid recordingId,
                ReplaySessionManager manager,
                RecordingCatalogStore catalog,
                CsvRecordingImporter importer) =>
            {
                if (recordingId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        Error = "A known-good recording is required."
                    });
                }

                if (!catalog.TryGet(recordingId, out var entry) || entry is null)
                {
                    return Results.NotFound(new
                    {
                        Error = "Known-good recording was not found."
                    });
                }

                var archivePath = catalog.GetArchiveFilePath(entry);
                if (!File.Exists(archivePath))
                {
                    return Results.Conflict(new
                    {
                        Error = "Known-good recording archive file is missing."
                    });
                }

                ImportedCsvRecording imported;
                try
                {
                    imported = importer.Import(
                        archivePath,
                        entry.ReadTimestampMapping(),
                        recordingId: entry.RecordingId);
                }
                catch (Exception exception) when (
                    exception is ArgumentException or FormatException or InvalidDataException)
                {
                    return Results.BadRequest(new { Error = exception.Message });
                }

                if (!string.Equals(
                        imported.Recording.Source.ContentHash,
                        entry.SourceContentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Conflict(new
                    {
                        Error = "Archived recording content does not match catalog metadata."
                    });
                }

                if (!manager.TryCompareRecording(
                        sessionId,
                        imported.Reader,
                        imported.Channels,
                        out var comparison,
                        out var error))
                {
                    return error is null
                        ? Results.NotFound(new { Error = "Replay session not found." })
                        : Results.BadRequest(new { Error = error });
                }

                return Results.Ok(RecordingComparisonResponse.From(
                    entry.RecordingId,
                    entry.Name,
                    comparison!));
            })
            .WithName("CompareSessionRecording")
            .Produces<RecordingComparisonResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // NAVIGATE TO THE NEXT / PREVIOUS CHANGE OF A SELECTED CHANNEL
        group.MapPost("/{sessionId:guid}/next-change", (
            Guid sessionId, ChangeNavigationRequest request, ReplaySessionManager manager) =>
            NavigateChange(sessionId, request, manager, forward: true));

        group.MapPost("/{sessionId:guid}/previous-change", (
            Guid sessionId, ChangeNavigationRequest request, ReplaySessionManager manager) =>
            NavigateChange(sessionId, request, manager, forward: false));

        return app;
    }
}
