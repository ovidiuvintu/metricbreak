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

export type AxisPin = "left" | "right";

export interface InvestigationUiState {
    selectedChannelIds: string[];
    rightAxisChannelIds: string[];
    selectedChannelNames?: string[];
    rightAxisChannelNames?: string[];
    axisPins?: Partial<Record<string, AxisPin>>;
    chartViewport?: {
        pageStart: number;
        dataZoomStartPercent: number;
        dataZoomEndPercent: number;
    } | null;
}

export interface InvestigationHit {
    sequenceNumber: number;
    breakpointId?: string | null;
    label?: string | null;
}

export interface ExportInvestigationRequest {
    investigationId?: string | null;
    name?: string | null;
    description?: string | null;
    projectId?: string | null;
    uiState?: InvestigationUiState | null;
    breakpointHit?: InvestigationHit | null;
}

export interface TimelineEntrySnapshot {
    timestampNanoseconds: number;
    sampleSequence: number;
    name: string;
    description: string | null;
}

export interface RecordingTimelineSnapshot {
    events: TimelineEntrySnapshot[];
    stages: TimelineEntrySnapshot[];
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