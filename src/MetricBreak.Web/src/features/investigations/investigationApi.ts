import { apiRequest } from "../../shared/api/httpClient";
import type {
    CreateBookmarkRequest,
    ExportInvestigationRequest,
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

    listWorkspaceInvestigations(
        recordingName?: string | null,
    ): Promise<{ investigations: InvestigationSummary[] }> {
        const query = recordingName?.trim()
            ? `?recordingName=${encodeURIComponent(recordingName.trim())}`
            : "";

        return apiRequest<{ investigations: InvestigationSummary[] }>(
            `/api/investigations${query}`,
        );
    },

    exportSession(
        sessionId: string,
        request?: ExportInvestigationRequest | null,
    ): Promise<ExportInvestigationResponse> {
        const projectId = request?.projectId?.trim();
        const query = projectId
            ? `?projectId=${encodeURIComponent(projectId)}`
            : "";

        return apiRequest<ExportInvestigationResponse>(
            `/api/sessions/${encodeURIComponent(sessionId)}/export${query}`,
            {
                method: "POST",
                body: JSON.stringify(request ?? {}),
            },
        );
    },

    getInvestigation(
        investigationId: string,
        projectId?: string | null,
    ): Promise<{
        schemaVersion: number;
        investigationId: string;
        metadata?: {
            name?: string | null;
            description?: string | null;
        };
        uiState?: {
            selectedChannelIds: string[];
            rightAxisChannelIds: string[];
            chartViewport?: {
                pageStart: number;
                dataZoomStartPercent: number;
                dataZoomEndPercent: number;
            } | null;
        } | null;
        session: { sequenceNumber: number };
    }> {
        const query = projectId?.trim()
            ? `?projectId=${encodeURIComponent(projectId.trim())}`
            : "";

        return apiRequest(
            `/api/investigations/${encodeURIComponent(investigationId)}${query}`,
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