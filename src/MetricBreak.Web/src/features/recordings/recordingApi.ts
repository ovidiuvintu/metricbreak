
import { ApiError, apiRequest } from "../../shared/api/httpClient";
import type { CreateSessionResponse } from "./recordingTypes";
import type { SessionState } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeResponse } from "./sampleTypes";

export interface TimestampPreview {
  columns: { index: number; name: string }[];
  suggestedColumnIndex: number;
  suggestedColumnName: string;
  suggestedFormat: string;
  ambiguous: boolean;
  appliedColumnIndex: number;
  appliedFormat: string;
  timestampOrigin: "Unix" | "Elapsed";
  preview: { sampleIndex: number; raw: string; normalized: string }[];
  note: string | null;
}

export const TIMESTAMP_FORMATS = [
  { value: "Iso8601", label: "ISO-8601" },
  { value: "UnixEpochSeconds", label: "Unix epoch seconds" },
  { value: "UnixEpochMilliseconds", label: "Unix epoch milliseconds" },
  { value: "UnixEpochMicroseconds", label: "Unix epoch microseconds" },
  { value: "RelativeElapsedSeconds", label: "Relative elapsed seconds" },
] as const;

async function postCsv(
  path: string,
  file: File,
  mapping?: { columnIndex: number; format: string },
): Promise<Response> {
  const formData = new FormData();
  formData.append("file", file);
  if (mapping) {
    formData.append("timestampColumn", String(mapping.columnIndex));
    formData.append("timestampFormat", mapping.format);
  }

  try {
    return await fetch(path, { method: "POST", body: formData });
  } catch {
    throw new ApiError(
      0,
      "NetworkError",
      "Cannot connect to the MetricBreak API.",
    );
  }
}

async function readError(response: Response, fallback: string): Promise<string> {
  const body: unknown = await response.json().catch(() => null);
  const error =
    body !== null && typeof body === "object"
      ? (body as Record<string, unknown>)
      : {};

  return typeof error.error === "string"
    ? error.error
    : typeof error.message === "string"
      ? error.message
      : fallback;
}

export const recordingApi = {
  /**
   * Imports a CSV recording and creates a replay session.
   *
   * POST /api/sessions
   */
  async importCsv(
    file: File,
    mapping?: { columnIndex: number; format: string },
  ): Promise<CreateSessionResponse> {
    const response = await postCsv("/api/sessions", file, mapping);

    if (!response.ok) {
      throw new ApiError(
        response.status,
        "ImportFailed",
        await readError(response, `CSV import failed (${response.status}).`),
      );
    }

    return (await response.json()) as CreateSessionResponse;
  },

  async previewTimestamp(
    file: File,
    mapping?: { columnIndex: number; format: string },
  ): Promise<TimestampPreview> {
    const response = await postCsv(
      "/api/sessions/timestamp-preview",
      file,
      mapping,
    );

    if (!response.ok) {
      throw new ApiError(
        response.status,
        "PreviewFailed",
        await readError(response, `Timestamp preview failed (${response.status}).`),
      );
    }

    return (await response.json()) as TimestampPreview;
  },

  getSamples(
    sessionId: string,
    start: number,
    count: number,
): Promise<SampleRangeResponse> {
    if (
        !Number.isSafeInteger(start) ||
        start < 0 ||
        !Number.isInteger(count) ||
        count < 1 ||
        count > 5000
    ) {
        throw new Error("Invalid sample range.");
    }

    return apiRequest<SampleRangeResponse>(
        `/api/sessions/${encodeURIComponent(sessionId)}/samples` +
        `?start=${start}&count=${count}`,
    );
},


  /**
   * Retrieves the current state of an existing replay session.
   *
   * GET /api/sessions/{sessionId}
   */
  getSession(sessionId: string): Promise<SessionState> {
    return apiRequest<SessionState>(
      `/api/sessions/${encodeURIComponent(sessionId)}`,
    );
  },

  getChannels(sessionId: string): Promise<ChannelMetadata[]> {
  return apiRequest<ChannelMetadata[]>(
    `/api/sessions/${encodeURIComponent(sessionId)}/channels`
  );
},
};
