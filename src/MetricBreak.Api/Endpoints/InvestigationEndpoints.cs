
using Microsoft.AspNetCore.Mvc;
using MetricBreak.Api.Investigations;
using MetricBreak.Api.Recordings;
using MetricBreak.Api.Sessions;
using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Endpoints;

internal static class InvestigationEndpoints
{
    private const string _investigationTag = "Investigations";

    public static IEndpointRouteBuilder MapInvestigationEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/investigations",
            (
                [FromQuery] string? recordingName,
                SessionInvestigationStore store) =>
            {
                var investigations = store.List(recordingName);
                return Results.Ok(new InvestigationsListResponse(investigations));
            })
            .WithName("ListWorkspaceInvestigations")
            .WithTags(_investigationTag)
            .Produces<InvestigationsListResponse>(StatusCodes.Status200OK);

        app.MapGet(
            "/api/investigations/{investigationId:guid}",
            (
                Guid investigationId,
                [FromQuery] Guid? projectId,
                SessionInvestigationStore store,
                ProjectWorkspaceStore projectStore) =>
            {
                ExportedInvestigation? investigation;

                if (projectId is Guid selectedProjectId)
                {
                    if (!projectStore.TryLoadInvestigation(
                            selectedProjectId,
                            investigationId,
                            out investigation,
                            out var error))
                    {
                        return error?.Code == "InvestigationNotFound"
                            ? Results.NotFound(ToErrorBody(error))
                            : Results.BadRequest(ToErrorBody(
                                error ?? new ProjectStoreError(
                                    "InvestigationLoadFailed",
                                    "Unable to load investigation.")));
                    }
                }
                else if (!store.TryLoad(investigationId, out investigation))
                {
                    return Results.NotFound();
                }

                return Results.Ok(InvestigationDocumentNormalizer.Normalize(investigation!));
            })
            .WithName("GetInvestigation")
            .WithTags(_investigationTag)
            .Produces<ExportedInvestigation>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost(
            "/api/investigations/{investigationId:guid}/open",
            (
                Guid investigationId,
                [FromQuery] Guid? projectId,
                [FromQuery] Guid? closeSessionId,
                SessionInvestigationStore store,
                ProjectWorkspaceStore projectStore,
                InvestigationOpenService investigationOpen,
                ReplaySessionManager sessionManager) =>
            {
                ExportedInvestigation? investigation;

                if (projectId is Guid selectedProjectId)
                {
                    if (!projectStore.TryLoadInvestigation(
                            selectedProjectId,
                            investigationId,
                            out investigation,
                            out var error))
                    {
                        return error?.Code == "InvestigationNotFound"
                            ? Results.NotFound(ToErrorBody(error))
                            : Results.BadRequest(ToErrorBody(
                                error ?? new ProjectStoreError(
                                    "InvestigationLoadFailed",
                                    "Unable to load investigation.")));
                    }
                }
                else if (!store.TryLoad(investigationId, out investigation))
                {
                    return Results.NotFound();
                }

                if (!investigationOpen.TryOpen(
                        investigation!,
                        projectId,
                        out var response,
                        out var openError)
                    || response is null)
                {
                    return Results.BadRequest(new { Error = openError });
                }

                if (closeSessionId is Guid previousSessionId)
                {
                    sessionManager.Remove(previousSessionId);
                }

                var touched = InvestigationPersistence.WithLastOpenedUtc(
                    investigation!,
                    DateTimeOffset.UtcNow);
                if (!InvestigationPersistence.TrySave(
                        touched,
                        projectId,
                        store,
                        projectStore,
                        out var persistError))
                {
                    return Results.BadRequest(ToErrorBody(
                        persistError ?? new ProjectStoreError(
                            "InvestigationSaveFailed",
                            "Unable to update investigation metadata.")));
                }

                return Results.Created(
                    $"/api/sessions/{response.SessionId}",
                    response);
            })
            .WithName("OpenInvestigation")
            .WithTags(_investigationTag)
            .Produces<OpenInvestigationResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        // EXPORT INVESTIGATION
        app.MapPost(
            "/api/sessions/{sessionId:guid}/export",
            (
                Guid sessionId,
                [FromQuery] Guid? projectId,
                [FromBody] ExportInvestigationRequest? request,
                ReplaySessionManager manager,
                SessionInvestigationStore store,
                ProjectWorkspaceStore projectStore,
                RecordingCatalogStore catalog) =>
            {
                var exportRequest = request ?? new ExportInvestigationRequest();
                if (projectId is Guid queryProjectId)
                {
                    exportRequest = exportRequest with { ProjectId = queryProjectId };
                }

                if (!manager.TryExportInvestigation(
                        sessionId,
                        exportRequest,
                        out var investigation)
                    || investigation is null)
                {
                    return Results.NotFound();
                }

                var prior = LoadPriorInvestigation(
                    investigation.InvestigationId,
                    exportRequest.ProjectId ?? projectId,
                    projectStore,
                    store);

                investigation = MergeExistingBookmarks(investigation, prior);
                investigation = MergeExistingTimeline(investigation, prior);
                investigation = InvestigationPersistence.MergePriorMetadata(
                    investigation,
                    prior);

                investigation = InvestigationDocumentNormalizer.Normalize(investigation);

                string filePath;

                var selectedProjectId = exportRequest.ProjectId ?? projectId;
                if (selectedProjectId is Guid resolvedProjectId)
                {
                    if (!projectStore.TrySaveInvestigation(
                            resolvedProjectId,
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

                    if (!TryResolveCatalogArchivePath(
                            catalog,
                            investigation,
                            out var workspaceArchivePath)
                        || workspaceArchivePath is null)
                    {
                        return Results.BadRequest(ToErrorBody(
                            new ProjectStoreError(
                                "RecordingArchiveMissing",
                                "Recording must be in the workspace catalog before saving to a project.")));
                    }

                    if (!projectStore.TryEnsureRecordingArchive(
                            resolvedProjectId,
                            investigation.Recording.RecordingId,
                            investigation.Recording.SourceContentHash,
                            investigation.Recording.SourceSizeInBytes,
                            investigation.Recording.SourceName,
                            workspaceArchivePath,
                            out var archiveError))
                    {
                        return Results.BadRequest(ToErrorBody(
                            archiveError ?? new ProjectStoreError(
                                "RecordingArchiveFailed",
                                "Unable to archive recording in the project.")));
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

        app.MapDelete(
            "/api/investigations/{investigationId:guid}",
            (
                Guid investigationId,
                [FromQuery] Guid? projectId,
                SessionInvestigationStore store,
                ProjectWorkspaceStore projectStore) =>
            {
                if (projectId is Guid selectedProjectId)
                {
                    if (!projectStore.TryDeleteInvestigation(
                            selectedProjectId,
                            investigationId,
                            out var deleteError))
                    {
                        return deleteError?.Code == "InvestigationNotFound"
                            ? Results.NotFound(ToErrorBody(deleteError))
                            : Results.BadRequest(ToErrorBody(
                                deleteError ?? new ProjectStoreError(
                                    "InvestigationDeleteFailed",
                                    "Unable to delete investigation.")));
                    }

                    return Results.NoContent();
                }

                if (!store.TryDelete(investigationId))
                    return Results.NotFound();

                return Results.NoContent();
            })
            .WithName("DeleteWorkspaceInvestigation")
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

    private static bool TryResolveCatalogArchivePath(
        RecordingCatalogStore catalog,
        ExportedInvestigation investigation,
        out string? archivePath)
    {
        archivePath = null;
        var recording = investigation.Recording;

        if (recording.RecordingId != Guid.Empty
            && catalog.TryGet(recording.RecordingId, out var byId)
            && byId is not null)
        {
            archivePath = catalog.GetArchiveFilePath(byId);
            return true;
        }

        if (catalog.TryFindBySource(
                recording.SourceContentHash,
                recording.SourceSizeInBytes,
                out var bySource)
            && bySource is not null)
        {
            archivePath = catalog.GetArchiveFilePath(bySource);
            return true;
        }

        return false;
    }

    private static ExportedInvestigation? LoadPriorInvestigation(
        Guid investigationId,
        Guid? projectId,
        ProjectWorkspaceStore projectStore,
        SessionInvestigationStore sessionStore)
    {
        if (projectId is Guid selectedProjectId)
        {
            projectStore.TryLoadInvestigation(
                selectedProjectId,
                investigationId,
                out var previous,
                out _);
            return previous;
        }

        sessionStore.TryLoad(investigationId, out var workspace);
        return workspace;
    }

    private static ExportedInvestigation MergeExistingBookmarks(
        ExportedInvestigation investigation,
        ExportedInvestigation? previous)
    {
        if (previous?.Bookmarks is not { Count: > 0 } bookmarks)
            return investigation;

        return investigation with { Bookmarks = bookmarks };
    }

    private static ExportedInvestigation MergeExistingTimeline(
        ExportedInvestigation investigation,
        ExportedInvestigation? previous)
    {
        if (investigation.Timeline is not null && !investigation.Timeline.IsEmpty)
            return investigation;

        if (previous?.Timeline is not { IsEmpty: false } priorTimeline)
            return investigation;

        return investigation with { Timeline = priorTimeline };
    }
}
