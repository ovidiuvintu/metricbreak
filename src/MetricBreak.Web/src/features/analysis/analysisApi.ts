import { apiRequest } from "../../shared/api/httpClient";
import type { WindowAnalysis } from "./analysisTypes";

export const analysisApi = {
  getWindow(
    sessionId: string,
    start: number,
    end: number,
    channelIds?: string[],
  ): Promise<WindowAnalysis> {
    const params = new URLSearchParams({
      start: String(start),
      end: String(end),
    });
    for (const channelId of channelIds ?? []) {
      params.append("channelId", channelId);
    }

    return apiRequest<WindowAnalysis>(
      `/api/sessions/${sessionId}/analysis?${params.toString()}`,
    );
  },
};
