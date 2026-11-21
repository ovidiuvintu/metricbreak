export interface InvestigationSummary {
    investigationId: string;
    recordingName: string;
    relativePath: string;
    exportedAtUtc: string;
    sourceContentHash: string;
    sourceSizeInBytes: number;
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