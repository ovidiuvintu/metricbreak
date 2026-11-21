
using Microsoft.AspNetCore.Mvc;
using MetricBreak.Api.Investigations;
using MetricBreak.Api.Sessions;
using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Endpoints;

internal static class InvestigationEndpoints
{
    private const string _investigationTag = "Investigations";

    public static IEndpointRouteBuilder MapInvestigationEndpoints(
        this IEndpointRouteBuilder app)
    {
        // EXPORT INVESTIGATION
        app.MapPost(
            "/api/sessions/{sessionId:guid}/export",
            (
                Guid sessionId,
                [FromQuery] Guid? projectId,
                ReplaySessionManager manager,
                SessionInvestigationStore store,
                ProjectWorkspaceStore projectStore) =>
            {
                if (!manager.TryExportInvestigation(
                        sessionId,
                        out var investigation)
                    || investigation is null)
                {
                    return Results.NotFound();
                }

                string filePath;

                if (projectId is Guid selectedProjectId)
                {
                    if (!projectStore.TrySaveInvestigation(
                            selectedProjectId,
                            investigation,
                            out var projectPath,
                            out var error)
                        || projectPath is null)
                    {
                        return Results.BadRequest(ToErrorBody(
                            error ?? new ProjectStoreError(
                                "InvestigationSaveFailed",
                                "Unable to save investigation to project.")));
                    }

                    filePath = projectPath;
                }
                else
                {
                    filePath = store.Save(investigation);
                }

                return Results.Created(
                    $"/api/investigations/{investigation.InvestigationId}",
                    new ExportSessionResponse(
                        investigation.InvestigationId,
                        filePath));
            })
            .WithName("ExportReplaySession")
            .WithTags(_investigationTag)
            .Produces<ExportSessionResponse>(
                StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound);

        // IMPORT INVESTIGATION
        app.MapPost(
            "/api/sessions/import",
            async (
                [FromForm] Guid? projectId,
                [FromForm] Guid investigationId,
                IFormFile file,
                ReplaySessionManager manager,
                SessionInvestigationStore store,
                ProjectWorkspaceStore projectStore,
                CsvSessionImportService importService,
                CancellationToken cancellationToken) =>
            {
                ExportedInvestigation? investigation;

                if (projectId is Guid selectedProjectId)
                {
                    if (!projectStore.TryLoadInvestigation(
                            selectedProjectId,
                            investigationId,
                            out investigation,
                            out var error)
                        || investigation is null)
                    {
                        return Results.NotFound(ToErrorBody(
                            error ?? new ProjectStoreError(
                                "InvestigationNotFound",
                                "Investigation not found in project.")));
                    }
                }
                else if (!store.TryLoad(
                             investigationId,
                             out investigation)
                         || investigation is null)
                {
                    return Results.NotFound();
                }

                try
                {
                    var imported =
                        await importService.ImportRecordingAsync(
                            file,
                            cancellationToken);

                    if (!manager.TryImportInvestigation(
                            imported,
                            investigation,
                            out var sessionId,
                            out var error))
                    {
                        return Results.BadRequest(new
                        {
                            Error = error ??
                                "Unable to import investigation."
                        });
                    }

                    if (!manager.TryGetSnapshot(
                            sessionId,
                            out var snapshot)
                        || snapshot is null)
                    {
                        return Results.Problem(
                            "Imported session was created but could not be read.");
                    }

                    return Results.Created(
                        $"/api/sessions/{sessionId}",
                        new CreateSessionResponse(
                            SessionId: sessionId,
                            RecordingId: snapshot.RecordingId,
                            RecordingName: snapshot.RecordingName,
                            SampleCount: snapshot.SampleCount,
                            Status: "Imported"));
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
            .WithName("ImportReplaySession")
            .WithTags(_investigationTag)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CreateSessionResponse>(
                StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
            "/api/investigations/{investigationId:guid}/bookmarks",
            (
                Guid investigationId,
                [FromQuery] Guid? projectId,
                InvestigationBookmarkService bookmarks) =>
            {
                if (!bookmarks.TryList(
                        projectId,
                        investigationId,
                        out var response,
                        out var error)
                    || response is null)
                {
                    return ToBookmarkErrorResult(error);
                }

                return Results.Ok(response);
            })
            .WithName("ListInvestigationBookmarks")
            .WithTags(_investigationTag)
            .Produces<InvestigationBookmarksResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPost(
            "/api/investigations/{investigationId:guid}/bookmarks",
            (
                Guid investigationId,
                [FromQuery] Guid? projectId,
                CreateInvestigationBookmarkRequest request,
                InvestigationBookmarkService bookmarks) =>
            {
                if (!bookmarks.TryCreate(
                        projectId,
                        investigationId,
                        request,
                        out var bookmark,
                        out var error)
                    || bookmark is null)
                {
                    return ToBookmarkErrorResult(error);
                }

                return Results.Created(
                    $"/api/investigations/{investigationId}/bookmarks/{bookmark.BookmarkId}",
                    bookmark);
            })
            .WithName("CreateInvestigationBookmark")
            .WithTags(_investigationTag)
            .Produces<InvestigationBookmarkItemResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPut(
            "/api/investigations/{investigationId:guid}/bookmarks/{bookmarkId:guid}",
            (
                Guid investigationId,
                Guid bookmarkId,
                [FromQuery] Guid? projectId,
                UpdateInvestigationBookmarkRequest request,
                InvestigationBookmarkService bookmarks) =>
            {
                if (!bookmarks.TryUpdate(
                        projectId,
                        investigationId,
                        bookmarkId,
                        request,
                        out var bookmark,
                        out var error)
                    || bookmark is null)
                {
                    return ToBookmarkErrorResult(error);
                }

                return Results.Ok(bookmark);
            })
            .WithName("UpdateInvestigationBookmark")
            .WithTags(_investigationTag)
            .Produces<InvestigationBookmarkItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapDelete(
            "/api/investigations/{investigationId:guid}/bookmarks/{bookmarkId:guid}",
            (
                Guid investigationId,
                Guid bookmarkId,
                [FromQuery] Guid? projectId,
                InvestigationBookmarkService bookmarks) =>
            {
                if (!bookmarks.TryDelete(
                        projectId,
                        investigationId,
                        bookmarkId,
                        out var error))
                {
                    return ToBookmarkErrorResult(error);
                }

                return Results.NoContent();
            })
            .WithName("DeleteInvestigationBookmark")
            .WithTags(_investigationTag)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static IResult ToBookmarkErrorResult(ProjectStoreError? error)
    {
        if (error is null)
            return Results.BadRequest(new { Error = "Request failed." });

        if (error.Code is "InvestigationNotFound" or "BookmarkNotFound")
        {
            return Results.NotFound(ToErrorBody(error));
        }

        return Results.BadRequest(ToErrorBody(error));
    }

    private static object ToErrorBody(ProjectStoreError error)
    {
        return new
        {
            ErrorCode = error.Code,
            Error = error.Message
        };
    }
}
