
using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Endpoints;

internal static class ProjectEndpoints
{
    private const string _projectTag = "Projects";
    public static WebApplication MapProjectEndpoints(
        this WebApplication app)
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
            .WithTags(_projectTag)
            .Produces<ProjectSummaryResponse>(
                StatusCodes.Status201Created)
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
            .WithTags(_projectTag)
            .Produces<ProjectSummaryResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet(
            "/api/projects/open",
            (ProjectWorkspaceStore projectStore) =>
            {
                return Results.Ok(
                    projectStore.GetOpenProjects());
            })
            .WithName("GetOpenProjects")
            .WithTags(_projectTag)
            .Produces<IReadOnlyList<ProjectSummaryResponse>>(
                StatusCodes.Status200OK);

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
            .WithTags(_projectTag)
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

                return Results.Ok(
                    new ProjectInvestigationsResponse(
                        investigations));
            })
            .WithName("ListProjectInvestigations")
            .WithTags(_projectTag)
            .Produces<ProjectInvestigationsResponse>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
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

