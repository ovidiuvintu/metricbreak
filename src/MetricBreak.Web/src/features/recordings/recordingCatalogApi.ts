import { apiRequest } from "../../shared/api/httpClient";
import type { CreateSessionResponse } from "./recordingTypes";

export interface RecordingCatalogItem {
  recordingId: string;
  name: string;
  sourceName: string;
  sourceContentHash: string;
  sourceSizeInBytes: number;
  sampleCount: number;
  importedAtUtc: string;
}

export const recordingCatalogApi = {
  list(): Promise<{ recordings: RecordingCatalogItem[] }> {
    return apiRequest<{ recordings: RecordingCatalogItem[] }>("/api/recordings");
  },

  open(recordingId: string): Promise<CreateSessionResponse> {
    return apiRequest<CreateSessionResponse>(
      `/api/recordings/${encodeURIComponent(recordingId)}/open`,
      { method: "POST" },
    );
  },
};