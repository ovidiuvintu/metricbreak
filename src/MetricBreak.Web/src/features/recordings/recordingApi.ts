
import { ApiError, apiRequest } from "../../shared/api/httpClient";
import type { CreateSessionResponse } from "./recordingTypes";
import type { SessionState } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeResponse } from "./sampleTypes";

export const recordingApi = {
  /**
   * Imports a CSV recording and creates a replay session.
   *
   * POST /api/sessions
   */
  async importCsv(file: File): Promise<CreateSessionResponse> {
    const formData = new FormData();
    formData.append("file", file);

    let response: Response;

    try {
      response = await fetch("/api/sessions", {
        method: "POST",
        body: formData,
      });
    } catch {
      throw new ApiError(
        0,
        "NetworkError",
        "Cannot connect to the MetricBreak API.",
      );
    }

    if (!response.ok) {
      const body: unknown = await response.json().catch(() => null);

      const error =
        body !== null && typeof body === "object"
          ? (body as Record<string, unknown>)
          : {};

      const message =
        typeof error.error === "string"
          ? error.error
          : typeof error.message === "string"
            ? error.message
            : `CSV import failed (${response.status}).`;

      throw new ApiError(
        response.status,
        "ImportFailed",
        message,
      );
    }

    return (await response.json()) as CreateSessionResponse;
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
