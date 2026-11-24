import { apiRequest } from "../../shared/api/httpClient";
import type { EventAnalysis, RecordingComparison, WindowAnalysis } from "./analysisTypes";

export const analysisApi = {
  getWindow(
    sessionId: string,
    start: number,
    end: number,
    channelIds?: string[],
    cadenceNanoseconds?: string | null,
  ): Promise<WindowAnalysis> {
    const params = new URLSearchParams({
      start: String(start),
      end: String(end),
    });
    for (const channelId of channelIds ?? []) {
      params.append("channelId", channelId);
    }
    if (cadenceNanoseconds) {
      params.set("cadenceNanoseconds", cadenceNanoseconds);
    }

    return apiRequest<WindowAnalysis>(
      `/api/sessions/${sessionId}/analysis?${params.toString()}`,
    );
  },

  getEvent(
    sessionId: string,
    sequence: number,
    lookbackSamples: number,
  ): Promise<EventAnalysis> {
    const params = new URLSearchParams({
      sequence: String(sequence),
      lookback: String(lookbackSamples),
    });
    return apiRequest<EventAnalysis>(
      `/api/sessions/${sessionId}/analysis/event?${params.toString()}`,
    );
  },

  compareRecording(
    sessionId: string,
    recordingId: string,
  ): Promise<RecordingComparison> {
    const params = new URLSearchParams({ recordingId });
    return apiRequest<RecordingComparison>(
      `/api/sessions/${sessionId}/analysis/compare?${params.toString()}`,
    );
  },
};
