using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Investigations;

public sealed class InvestigationBookmarkService
{
    private readonly ProjectWorkspaceStore _projectStore;
    private readonly SessionInvestigationStore _sessionStore;

    public InvestigationBookmarkService(
        ProjectWorkspaceStore projectStore,
        SessionInvestigationStore sessionStore)
    {
        _projectStore = projectStore;
        _sessionStore = sessionStore;
    }

    public bool TryList(
        Guid? projectId,
        Guid investigationId,
        out InvestigationBookmarksResponse? response,
        out ProjectStoreError? error)
    {
        response = null;
        error = null;

        if (!TryLoad(projectId, investigationId, out var investigation, out error)
            || investigation is null)
        {
            return false;
        }

        investigation = InvestigationDocumentNormalizer.Normalize(investigation);

        response = new InvestigationBookmarksResponse(
            InvestigationBookmarkMutations.Sort(investigation.Bookmarks!)
                .Select(bookmark => ToResponse(investigation.InvestigationId, bookmark))
                .ToArray());

        return true;
    }

    public bool TryCreate(
        Guid? projectId,
        Guid investigationId,
        CreateInvestigationBookmarkRequest request,
        out InvestigationBookmarkItemResponse? bookmark,
        out ProjectStoreError? error)
    {
        bookmark = null;
        error = null;

        ArgumentNullException.ThrowIfNull(request);

        if (!TryLoad(projectId, investigationId, out var investigation, out error)
            || investigation is null)
        {
            return false;
        }

        investigation = InvestigationBookmarkMutations.Normalize(investigation);

        if (!InvestigationBookmarkMutations.TryValidateSequence(
                investigation,
                request.SampleSequence,
                out var validationError))
        {
            error = new ProjectStoreError(
                "InvalidSampleSequence",
                validationError ?? "Sample sequence is invalid.");
            return false;
        }

        var snapshot = InvestigationBookmarkMutations.Create(
            request.SampleSequence,
            request.TimestampNanoseconds,
            request.Title,
            request.Annotation);

        var updated = InvestigationBookmarkMutations.Add(investigation, snapshot);

        if (!TrySave(projectId, updated, out error))
            return false;

        bookmark = ToResponse(updated.InvestigationId, snapshot);
        return true;
    }

    public bool TryUpdate(
        Guid? projectId,
        Guid investigationId,
        Guid bookmarkId,
        UpdateInvestigationBookmarkRequest request,
        out InvestigationBookmarkItemResponse? bookmark,
        out ProjectStoreError? error)
    {
        bookmark = null;
        error = null;

        ArgumentNullException.ThrowIfNull(request);

        if (!TryLoad(projectId, investigationId, out var investigation, out error)
            || investigation is null)
        {
            return false;
        }

        investigation = InvestigationBookmarkMutations.Normalize(investigation);

        if (!InvestigationBookmarkMutations.TryUpdate(
                investigation,
                bookmarkId,
                request.Title,
                request.Annotation,
                out var updated,
                out var mutationError)
            || updated is null)
        {
            error = new ProjectStoreError(
                "BookmarkNotFound",
                mutationError ?? "Bookmark was not found.");
            return false;
        }

        if (!TrySave(projectId, updated, out error))
            return false;

        var snapshot = updated.Bookmarks!
            .First(bookmark => bookmark.BookmarkId == bookmarkId);

        bookmark = ToResponse(updated.InvestigationId, snapshot);
        return true;
    }

    public bool TryDelete(
        Guid? projectId,
        Guid investigationId,
        Guid bookmarkId,
        out ProjectStoreError? error)
    {
        error = null;

        if (!TryLoad(projectId, investigationId, out var investigation, out error)
            || investigation is null)
        {
            return false;
        }

        investigation = InvestigationBookmarkMutations.Normalize(investigation);

        if (!InvestigationBookmarkMutations.TryDelete(
                investigation,
                bookmarkId,
                out var updated,
                out var mutationError)
            || updated is null)
        {
            error = new ProjectStoreError(
                "BookmarkNotFound",
                mutationError ?? "Bookmark was not found.");
            return false;
        }

        return TrySave(projectId, updated, out error);
    }

    private bool TryLoad(
        Guid? projectId,
        Guid investigationId,
        out ExportedInvestigation? investigation,
        out ProjectStoreError? error)
    {
        investigation = null;
        error = null;

        if (projectId is Guid selectedProjectId)
        {
            if (!_projectStore.TryLoadInvestigation(
                    selectedProjectId,
                    investigationId,
                    out investigation,
                    out error))
            {
                return false;
            }

            investigation = InvestigationBookmarkMutations.Normalize(investigation!);
            return true;
        }

        if (!_sessionStore.TryLoad(investigationId, out investigation)
            || investigation is null)
        {
            error = new ProjectStoreError(
                "InvestigationNotFound",
                "Investigation was not found.");
            return false;
        }

        investigation = InvestigationBookmarkMutations.Normalize(investigation);
        return true;
    }

    private bool TrySave(
        Guid? projectId,
        ExportedInvestigation investigation,
        out ProjectStoreError? error)
    {
        error = null;

        if (projectId is Guid selectedProjectId)
        {
            if (!_projectStore.TrySaveInvestigation(
                    selectedProjectId,
                    investigation,
                    out _,
                    out error))
            {
                return false;
            }

            return true;
        }

        _sessionStore.Save(investigation);
        return true;
    }

    private static InvestigationBookmarkItemResponse ToResponse(
        Guid investigationId,
        InvestigationBookmarkSnapshot bookmark)
    {
        return new InvestigationBookmarkItemResponse(
            bookmark.BookmarkId,
            investigationId,
            bookmark.SampleSequence,
            bookmark.TimestampNanoseconds,
            bookmark.Title,
            bookmark.Annotation,
            bookmark.CreatedAtUtc,
            bookmark.ModifiedAtUtc);
    }
}