using MetricBreak.Api.Recordings;
using MetricBreak.Api.Sessions;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Endpoints;

internal static class RecordingEndpoints
{
    private const string _recordingTag = "Recordings";

    public static IEndpointRouteBuilder MapRecordingEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/recordings",
            (RecordingCatalogStore catalog) =>
                Results.Ok(new RecordingListResponse(catalog.List())))
            .WithName("ListRecordings")
            .WithTags(_recordingTag)
            .Produces<RecordingListResponse>(StatusCodes.Status200OK);

        app.MapPost(
            "/api/recordings/{recordingId:guid}/open",
            (
                Guid recordingId,
                RecordingCatalogStore catalog,
                ReplaySessionManager manager,
                CsvRecordingImporter importer) =>
            {
                if (!catalog.TryGet(recordingId, out var entry) || entry is null)
                    return Results.NotFound();

                var archivePath = catalog.GetArchiveFilePath(entry);
                if (!File.Exists(archivePath))
                {
                    return Results.Problem(
                        "Recording archive file is missing.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                ImportedCsvRecording imported;
                try
                {
                    imported = importer.Import(
                        archivePath,
                        mapping: null,
                        recordingId: entry.RecordingId);
                }
                catch (Exception exception) when (
                    exception is ArgumentException or FormatException or InvalidDataException)
                {
                    return Results.BadRequest(new { Error = exception.Message });
                }

                if (imported.Recording.Source.ContentHash != entry.SourceContentHash)
                {
                    return Results.Conflict(new
                    {
                        Error = "Archived recording content does not match catalog metadata.",
                    });
                }

                var sessionId = manager.Create(imported);

                return Results.Created(
                    $"/api/sessions/{sessionId}",
                    new CreateSessionResponse(
                        sessionId,
                        imported.Recording.Id,
                        imported.Recording.Name,
                        imported.Reader.SampleCount,
                        "Ready"));
            })
            .WithName("OpenCatalogRecording")
            .WithTags(_recordingTag)
            .Produces<CreateSessionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record RecordingListResponse(
    IReadOnlyList<RecordingCatalogItemResponse> Recordings);