export interface InvestigationSummary {
    investigationId: string;
    investigationName?: string;
    recordingName: string;
    recordingId?: string;
    relativePath: string;
    exportedAtUtc: string;
    lastOpenedAtUtc?: string;
    sourceContentHash: string;
    sourceSizeInBytes: number;
}

export interface InvestigationUiState {
    selectedChannelIds: string[];
    rightAxisChannelIds: string[];
    chartViewport?: {
        pageStart: number;
        dataZoomStartPercent: number;
        dataZoomEndPercent: number;
    } | null;
}

export interface ExportInvestigationRequest {
    investigationId?: string | null;
    name?: string | null;
    description?: string | null;
    projectId?: string | null;
    uiState?: InvestigationUiState | null;
}

export interface OpenInvestigationResponse {
    sessionId: string;
    investigationId: string;
    recordingId: string;
    recordingName: string;
    investigationName: string | null;
    sampleCount: number;
    sequenceNumber: number;
    status: string;
}

export interface InvestigationBookmark {
    bookmarkId: string;
    investigationId: string;
    sampleSequence: number;
    timestampNanoseconds: number;
    title: string | null;
    annotation: string | null;
    createdAtUtc: string;
    modifiedAtUtc: string;
}

export interface CreateBookmarkRequest {
    sampleSequence: number;
    timestampNanoseconds: number;
    title?: string | null;
    annotation?: string | null;
}

export interface UpdateBookmarkRequest {
    title?: string | null;
    annotation?: string | null;
}

export interface ExportInvestigationResponse {
    investigationId: string;
    filePath: string;
}