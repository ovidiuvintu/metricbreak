using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Replay;
using Microsoft.AspNetCore.Mvc;

namespace MetricBreak.Api;

internal static class ApiTags
{
    public const string Projects = "Projects";
    public const string Sessions = "Sessions";
    public const string ReplayControls = "Replay Controls";
    public const string Investigations = "Investigations";
    public const string Breakpoints = "Breakpoints";
}

internal static class EndpointMappings
{
    private static object ToErrorBody(ProjectStoreError error)
    {
        return new
        {
            ErrorCode = error.Code,
            Error = error.Message
        };
    }

    public static WebApplication MapProjectEndpoints(this WebApplication app)
    {
        app.MapPost(
            "/api/projects",
            (
                CreateProjectRequest request,
                ProjectWorkspaceStore projectStore) =>
            {
                if (!projectStore.TryCreateProject(
                        request.ProjectName,
                        request.RootPath,
                        out var project,
                        out var error)
                    || project is null)
                {
                    return Results.BadRequest(ToErrorBody(
                        error ?? new ProjectStoreError(
                            "ProjectCreateFailed",
                            "Unable to create project.")));
                }

                return Results.Created(
                    $"/api/projects/{project.ProjectId}",
                    project);
            })
            .WithName("CreateProject")
            .WithTags(ApiTags.Projects)
            .Produces<ProjectSummaryResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPost(
            "/api/projects/open",
            (
                OpenProjectRequest request,
                ProjectWorkspaceStore projectStore) =>
            {
                if (!projectStore.TryOpenProject(
                        request.RootPath,
                        out var project,
                        out var error)
                    || project is null)
                {
                    return Results.NotFound(ToErrorBody(
                        error ?? new ProjectStoreError(
                            "ProjectOpenFailed",
                            "Project not found.")));
                }

                return Results.Ok(project);
            })
            .WithName("OpenProject")
            .WithTags(ApiTags.Projects)
            .Produces<ProjectSummaryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
            "/api/projects/open",
            (ProjectWorkspaceStore projectStore) =>
            {
                return Results.Ok(projectStore.GetOpenProjects());
            })
            .WithName("GetOpenProjects")
            .WithTags(ApiTags.Projects)
            .Produces<IReadOnlyList<ProjectSummaryResponse>>(StatusCodes.Status200OK);

        app.MapPost(
            "/api/projects/{projectId:guid}/close",
            (
                Guid projectId,
                ProjectWorkspaceStore projectStore) =>
            {
                if (!projectStore.TryCloseProject(
                        projectId,
                        out var error))
                {
                    return Results.NotFound(ToErrorBody(
                        error ?? new ProjectStoreError(
                            "ProjectCloseFailed",
                            "Project was not open.")));
                }

                return Results.NoContent();
            })
            .WithName("CloseProject")
            .WithTags(ApiTags.Projects)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
            "/api/projects/{projectId:guid}/investigations",
            (
                Guid projectId,
                ProjectWorkspaceStore projectStore) =>
            {
                if (!projectStore.TryListInvestigations(
                        projectId,
                        out var investigations,
                        out var error)
                    || investigations is null)
                {
                    return Results.NotFound(ToErrorBody(
                        error ?? new ProjectStoreError(
                            "ProjectNotFound",
                            "Project not found.")));
                }

                return Results.Ok(new ProjectInvestigationsResponse(
                    investigations));
            })
            .WithName("ListProjectInvestigations")
            .WithTags(ApiTags.Projects)
            .Produces<ProjectInvestigationsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    public static WebApplication MapSessionEndpoints(this WebApplication app)
    {
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
            .WithTags(ApiTags.Sessions)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CreateSessionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

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

                return Results.Ok(
                    SessionStateResponseMapper.Map(snapshot));
            })
            .WithName("GetReplaySessionState")
            .WithTags(ApiTags.Sessions)
            .Produces<SessionStateResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    public static WebApplication MapReplayEndpoints(this WebApplication app)
    {
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
            .WithTags(ApiTags.ReplayControls);

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
            .WithTags(ApiTags.ReplayControls);

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
            .WithTags(ApiTags.ReplayControls);

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
            .WithTags(ApiTags.ReplayControls);

        app.MapPost(
            "/api/sessions/{sessionId:guid}/continue",
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

                var session = SessionStateResponseMapper.Map(snapshot);

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
            .WithTags(ApiTags.ReplayControls)
            .Produces<ContinueResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost(
            "/api/sessions/{sessionId:guid}/continue-backward",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                var found = manager.TryContinueBackwardWithSnapshot(
                    sessionId,
                    out var hadActiveBreakpoints,
                    out var stop,
                    out var snapshot);

                if (!found || snapshot is null)
                    return Results.NotFound();

                var session = SessionStateResponseMapper.Map(snapshot);

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
            .WithTags(ApiTags.ReplayControls)
            .Produces<ContinueResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    public static WebApplication MapInvestigationEndpoints(this WebApplication app)
    {
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
            .WithTags(ApiTags.Investigations)
            .Produces<ExportSessionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound);

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
                    var imported = await importService.ImportRecordingAsync(
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
                            Error = error ?? "Unable to import investigation."
                        });
                    }

                    if (!manager.TryGetSnapshot(
                            sessionId,
                            out var snapshot)
                        || snapshot is null)
                    {
                        return Results.Problem("Imported session was created but could not be read.");
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
            .WithTags(ApiTags.Investigations)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CreateSessionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    public static WebApplication MapBreakpointEndpoints(this WebApplication app)
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
                        return Results.Problem("Breakpoint was not created.");

                    var response = BreakpointResponseMapper.Map(
                        breakpoint);

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
            .WithTags(ApiTags.Breakpoints)
            .Produces<BreakpointResponse>(StatusCodes.Status201Created)
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
            .WithTags(ApiTags.Breakpoints)
            .Produces<BreakpointsResponse>(StatusCodes.Status200OK)
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

                if (!found)
                    return Results.NotFound();

                if (!removed)
                    return Results.NotFound();

                return Results.NoContent();
            })
            .WithName("DeleteSessionBreakpoint")
            .WithTags(ApiTags.Breakpoints)
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

                if (!found)
                    return Results.NotFound();

                if (!updated)
                    return Results.NotFound();

                return Results.NoContent();
            })
            .WithName("SetSessionBreakpointEnabled")
            .WithTags(ApiTags.Breakpoints)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
