import { apiRequest } from "../../shared/api/httpClient";
import type {
    CreateBookmarkRequest,
    ExportInvestigationResponse,
    InvestigationBookmark,
    InvestigationSummary,
    UpdateBookmarkRequest,
} from "./investigationTypes";

export const investigationApi = {
    listProjectInvestigations(
        projectId: string,
    ): Promise<{ investigations: InvestigationSummary[] }> {
        return apiRequest<{ investigations: InvestigationSummary[] }>(
            `/api/projects/${encodeURIComponent(projectId)}/investigations`,
        );
    },

    exportSession(
        sessionId: string,
        projectId: string,
    ): Promise<ExportInvestigationResponse> {
        return apiRequest<ExportInvestigationResponse>(
            `/api/sessions/${encodeURIComponent(sessionId)}/export?projectId=${encodeURIComponent(projectId)}`,
            { method: "POST" },
        );
    },

    listBookmarks(
        investigationId: string,
        projectId?: string | null,
    ): Promise<{ bookmarks: InvestigationBookmark[] }> {
        const query = projectId
            ? `?projectId=${encodeURIComponent(projectId)}`
            : "";

        return apiRequest<{ bookmarks: InvestigationBookmark[] }>(
            `/api/investigations/${encodeURIComponent(investigationId)}/bookmarks${query}`,
        );
    },

    createBookmark(
        investigationId: string,
        request: CreateBookmarkRequest,
        projectId?: string | null,
    ): Promise<InvestigationBookmark> {
        const query = projectId
            ? `?projectId=${encodeURIComponent(projectId)}`
            : "";

        return apiRequest<InvestigationBookmark>(
            `/api/investigations/${encodeURIComponent(investigationId)}/bookmarks${query}`,
            {
                method: "POST",
                body: JSON.stringify(request),
            },
        );
    },

    updateBookmark(
        investigationId: string,
        bookmarkId: string,
        request: UpdateBookmarkRequest,
        projectId?: string | null,
    ): Promise<InvestigationBookmark> {
        const query = projectId
            ? `?projectId=${encodeURIComponent(projectId)}`
            : "";

        return apiRequest<InvestigationBookmark>(
            `/api/investigations/${encodeURIComponent(investigationId)}/bookmarks/${encodeURIComponent(bookmarkId)}${query}`,
            {
                method: "PUT",
                body: JSON.stringify(request),
            },
        );
    },

    deleteBookmark(
        investigationId: string,
        bookmarkId: string,
        projectId?: string | null,
    ): Promise<void> {
        const query = projectId
            ? `?projectId=${encodeURIComponent(projectId)}`
            : "";

        return apiRequest<void>(
            `/api/investigations/${encodeURIComponent(investigationId)}/bookmarks/${encodeURIComponent(bookmarkId)}${query}`,
            { method: "DELETE" },
        );
    },
};